using Cuisinier.Shared.DTOs;
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
            _logger.LogInformation("Starting detailed recipe generation for Recipe {RecipeId} in Menu {MenuId}", recipeId, menuId);
            
            // Each recipe gets its own scope to avoid DbContext threading issues
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
            var openAIService = scope.ServiceProvider.GetRequiredService<IOpenAIService>();

            var recipe = await context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null || !string.IsNullOrEmpty(recipe.DetailedRecipe))
            {
                _logger.LogInformation("Recipe {RecipeId} is null or already has DetailedRecipe, skipping", recipeId);
                return;
            }

            _logger.LogInformation("Generating detailed recipe for Recipe {RecipeId}", recipeId);
            var recipeResponse = recipe.ToResponse();
            var detailedRecipe = await openAIService.GenerateDetailedRecipeAsync(
                recipe.Title,
                recipeResponse.Ingredients,
                recipe.Description
            );

            _logger.LogInformation("Detailed recipe generated for Recipe {RecipeId}, saving to database", recipeId);
            recipe.DetailedRecipe = detailedRecipe;
            context.Entry(recipe).Property(r => r.DetailedRecipe).IsModified = true;
            
            // Update associated Dish if it exists (don't fail if this fails)
            try
            {
                var dishIdToUpdate = recipe.DishId ?? recipe.OriginalDishId;
                if (dishIdToUpdate.HasValue)
                {
                    _logger.LogInformation("Recipe {RecipeId} has DishId {DishId}, updating Dish", recipeId, dishIdToUpdate.Value);
                    var dish = await context.Dishes
                        .FirstOrDefaultAsync(d => d.Id == dishIdToUpdate.Value);
                    
                    if (dish != null)
                    {
                        dish.DetailedRecipe = detailedRecipe;
                        dish.UpdatedAt = DateTime.UtcNow;
                        context.Entry(dish).Property(d => d.DetailedRecipe).IsModified = true;
                        context.Entry(dish).Property(d => d.UpdatedAt).IsModified = true;
                        
                        _logger.LogInformation(
                            "Updated DetailedRecipe for Dish {DishId} associated with Recipe {RecipeId}",
                            dishIdToUpdate.Value,
                            recipeId);
                    }
                    else
                    {
                        _logger.LogWarning("Dish {DishId} not found for Recipe {RecipeId}", dishIdToUpdate.Value, recipeId);
                    }
                }
                else
                {
                    _logger.LogInformation("Recipe {RecipeId} has no DishId or OriginalDishId, skipping Dish update", recipeId);
                }
            }
            catch (Exception dishEx)
            {
                // Log but don't fail the recipe generation if dish update fails
                _logger.LogWarning(dishEx, 
                    "Failed to update Dish for Recipe {RecipeId}, but continuing with recipe update",
                    recipeId);
            }
            
            await context.SaveChangesAsync();
            _logger.LogInformation("Saved DetailedRecipe for Recipe {RecipeId} to database", recipeId);

            // Reload recipe with all relationships for mapping
            var completeRecipe = await context.Recipes
                .Include(r => r.Ingredients)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (completeRecipe != null)
            {
                // Get menu to find the userId for cache invalidation
                var menu = await context.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Id == menuId);
                if (menu != null)
                {
                    // Invalidate cache for the menu owner
                    _cache.Remove($"{MenuCacheKeyPrefix}{menu.UserId}_{menuId}");
                    _cache.Remove($"{AllMenusCacheKey}_{menu.UserId}");
                    _logger.LogInformation("Cache invalidated for Menu {MenuId}, UserId {UserId}", menuId, menu.UserId);
                }

                // Send update via SignalR
                _logger.LogInformation("Sending SignalR notification for Recipe {RecipeId}", recipeId);
                var updatedRecipe = completeRecipe.ToResponse();
                await _hubContext.Clients.Group($"menu-{menuId}")
                    .SendAsync("RecipeDetailedGenerated", updatedRecipe);
                _logger.LogInformation("SignalR notification sent for Recipe {RecipeId}", recipeId);
            }
            else
            {
                _logger.LogWarning("Could not reload Recipe {RecipeId} after saving", recipeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during detailed recipe generation for {RecipeId}", recipeId);
            // Send error notification
            try
            {
                await _hubContext.Clients.Group($"menu-{menuId}")
                    .SendAsync("RecipeGenerationError", recipeId);
            }
            catch (Exception hubEx)
            {
                _logger.LogError(hubEx, "Failed to send SignalR error notification for Recipe {RecipeId}", recipeId);
            }
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

