using Cuisinier.Core.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Mappings;
using Cuisinier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Cuisinier.Api.Hubs;

namespace Cuisinier.Api.Services;

public class BackgroundRecipeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<RecipeHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BackgroundRecipeService> _logger;

    private const string MenuCacheKeyPrefix = "Menu_";
    private const string AllMenusCacheKey = "Menu_All";

    public BackgroundRecipeService(
        IServiceScopeFactory scopeFactory,
        IHubContext<RecipeHub> hubContext,
        IMemoryCache cache,
        ILogger<BackgroundRecipeService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _cache = cache;
        _logger = logger;
    }

    public Task GenerateDetailedRecipesAsync(int menuId, List<int> recipeIds)
    {
        // Launch in background without waiting
        _ = Task.Run(async () =>
        {
            try
            {
                // Generate all recipes in parallel
                var tasks = recipeIds.Select(recipeId => GenerateSingleRecipeAsync(menuId, recipeId));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background recipe generation service");
            }
        });

        return Task.CompletedTask;
    }

    private async Task GenerateSingleRecipeAsync(int menuId, int recipeId)
    {
        try
        {
            // Each recipe gets its own scope to avoid DbContext threading issues
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
            var openAIService = scope.ServiceProvider.GetRequiredService<IOpenAIService>();

            var recipe = await context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null || !string.IsNullOrEmpty(recipe.DetailedRecipe))
            {
                return;
            }

            var recipeResponse = recipe.ToResponse();
            var detailedRecipe = await openAIService.GenerateDetailedRecipeAsync(
                recipe.Title,
                recipeResponse.Ingredients,
                recipe.Description
            );

            recipe.DetailedRecipe = detailedRecipe;
            context.Entry(recipe).Property(r => r.DetailedRecipe).IsModified = true;
            await context.SaveChangesAsync();

            // Reload recipe with all relationships for mapping
            var completeRecipe = await context.Recipes
                .Include(r => r.Ingredients)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (completeRecipe != null)
            {
                // Invalidate cache to ensure fresh data on next load
                _cache.Remove($"{MenuCacheKeyPrefix}{menuId}");
                _cache.Remove(AllMenusCacheKey);
                
                // Send update via SignalR
                var updatedRecipe = completeRecipe.ToResponse();
                await _hubContext.Clients.Group($"menu-{menuId}")
                    .SendAsync("RecipeDetailedGenerated", updatedRecipe);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during detailed recipe generation for {RecipeId}", recipeId);
            // Send error notification
            await _hubContext.Clients.Group($"menu-{menuId}")
                .SendAsync("RecipeGenerationError", recipeId);
        }
    }

    public Task GenerateShoppingListAsync(int menuId)
    {
        // Launch in background without waiting
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
                var openAIService = scope.ServiceProvider.GetRequiredService<IOpenAIService>();

                // Check if shopping list already exists
                var existingShoppingList = await context.ShoppingLists
                    .FirstOrDefaultAsync(l => l.MenuId == menuId);

                if (existingShoppingList != null)
                {
                    return;
                }

                var menu = await context.Menus
                    .Include(m => m.Recipes)
                        .ThenInclude(r => r.Ingredients)
                    .FirstOrDefaultAsync(m => m.Id == menuId);

                if (menu == null)
                {
                    return;
                }

                // Generate shopping list
                var recipesResponse = menu.Recipes.Select(r => r.ToResponse()).ToList();
                var shoppingListResponse = await openAIService.GenerateShoppingListAsync(recipesResponse);

                var shoppingList = new ShoppingList
                {
                    MenuId = menuId,
                    CreationDate = DateTime.UtcNow,
                    Items = shoppingListResponse.Items.Select(i => new ShoppingListItem
                    {
                        Name = i.Name,
                        Quantity = i.Quantity,
                        Category = i.Category,
                        IsManuallyAdded = i.IsManuallyAdded
                    }).ToList()
                };

                context.ShoppingLists.Add(shoppingList);
                await context.SaveChangesAsync();

                // Notify via SignalR that shopping list is ready
                await _hubContext.Clients.Group($"menu-{menuId}")
                    .SendAsync("ShoppingListGenerated", menuId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background shopping list generation service");
            }
        });

        return Task.CompletedTask;
    }
}

