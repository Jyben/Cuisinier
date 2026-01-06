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

public class BackgroundMenuService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<RecipeHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BackgroundMenuService> _logger;

    private const string MenuCacheKeyPrefix = "Menu_";
    private const string AllMenusCacheKey = "Menu_All";

    public BackgroundMenuService(
        IServiceScopeFactory scopeFactory,
        IHubContext<RecipeHub> hubContext,
        IMemoryCache cache,
        ILogger<BackgroundMenuService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _cache = cache;
        _logger = logger;
    }

    public Task GenerateMenuAsync(int menuId, MenuGenerationRequest request)
    {
        // Launch in background without waiting
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
                var openAIService = scope.ServiceProvider.GetRequiredService<IOpenAIService>();

                _logger.LogInformation(
                    "Starting background menu generation. MenuId: {MenuId}, WeekStartDate: {WeekStartDate}",
                    menuId,
                    request.Parameters.WeekStartDate);

                // Generate menu from OpenAI
                var menuResponse = await openAIService.GenerateMenuAsync(request.Parameters);

                // Use execution strategy to wrap transaction (required when EnableRetryOnFailure is enabled)
                var strategy = context.Database.CreateExecutionStrategy();
                
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await context.Database.BeginTransactionAsync();
                    try
                    {
                        // Parameters are already saved in StartMenuGenerationAsync, no need to save again

                        // Get menu (it should already exist with the provided ID)
                        var menu = await context.Menus
                            .Include(m => m.Recipes)
                            .FirstOrDefaultAsync(m => m.Id == menuId);

                        if (menu == null)
                        {
                            throw new InvalidOperationException($"Menu {menuId} not found. It should have been created before starting background generation.");
                        }

                        // If existing recipes are provided, reuse them
                        if (request.RecipeIds != null && request.RecipeIds.Any())
                        {
                            var existingRecipes = await context.Recipes
                                .Include(r => r.Ingredients)
                                .Where(r => request.RecipeIds.Contains(r.Id))
                                .ToListAsync();

                            foreach (var existingRecipe in existingRecipes)
                            {
                                var newRecipe = new Recipe
                                {
                                    MenuId = menu.Id,
                                    Title = existingRecipe.Title,
                                    Description = existingRecipe.Description,
                                    CompleteDescription = existingRecipe.CompleteDescription,
                                    DetailedRecipe = existingRecipe.DetailedRecipe,
                                    ImageUrl = existingRecipe.ImageUrl,
                                    PreparationTime = existingRecipe.PreparationTime,
                                    CookingTime = existingRecipe.CookingTime,
                                    Servings = existingRecipe.Servings,
                                    IsFromDatabase = true,
                                    OriginalDishId = existingRecipe.IsFromDatabase 
                                        ? existingRecipe.OriginalDishId ?? existingRecipe.Id 
                                        : existingRecipe.Id,
                                    Ingredients = existingRecipe.Ingredients.Select(i => new RecipeIngredient
                                    {
                                        Name = i.Name,
                                        Quantity = i.Quantity,
                                        Category = i.Category
                                    }).ToList()
                                };
                                menu.Recipes.Add(newRecipe);
                            }
                        }

                        // Add newly generated recipes
                        // Check for duplicates in favorites before adding
                        var allFavorites = await context.Favorites
                            .Include(f => f.Ingredients)
                            .ToListAsync();

                        foreach (var recipeResponse in menuResponse.Recipes)
                        {
                            var recipe = recipeResponse.ToEntity(menu.Id);
                            
                            // Check if this recipe already exists in favorites
                            var normalizedTitle = recipe.Title.Trim().ToLower();
                            var matchingFavorites = allFavorites
                                .Where(f => f.Title.Trim().ToLower() == normalizedTitle)
                                .ToList();

                            foreach (var favorite in matchingFavorites)
                            {
                                // Check if ingredients match
                                if (favorite.Ingredients.Count != recipe.Ingredients.Count)
                                    continue;

                                var favoriteIngredients = favorite.Ingredients
                                    .Select(i => (Name: i.Name.Trim().ToLower(), Quantity: i.Quantity.Trim().ToLower()))
                                    .OrderBy(i => i.Name)
                                    .ThenBy(i => i.Quantity)
                                    .ToList();

                                var recipeIngredients = recipe.Ingredients
                                    .Select(i => (Name: i.Name.Trim().ToLower(), Quantity: i.Quantity.Trim().ToLower()))
                                    .OrderBy(i => i.Name)
                                    .ThenBy(i => i.Quantity)
                                    .ToList();

                                if (favoriteIngredients.SequenceEqual(recipeIngredients, new IngredientEqualityComparer()))
                                {
                                    // Mark as from database and link to favorite
                                    recipe.IsFromDatabase = true;
                                    recipe.OriginalDishId = favorite.Id;
                                    _logger.LogInformation(
                                        "Recipe '{Title}' matches existing favorite (ID: {FavoriteId})",
                                        recipe.Title,
                                        favorite.Id);
                                    break;
                                }
                            }
                            
                            menu.Recipes.Add(recipe);
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation(
                            "Menu generated successfully. MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                            menu.Id,
                            menu.Recipes.Count);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                // Load with relationships for response (outside transaction)
                var completeMenu = await context.Menus
                    .Include(m => m.Recipes)
                        .ThenInclude(r => r.Ingredients)
                    .FirstOrDefaultAsync(m => m.Id == menuId);

                if (completeMenu != null)
                {
                    // Invalidate cache
                    _cache.Remove($"{MenuCacheKeyPrefix}{menuId}");
                    _cache.Remove(AllMenusCacheKey);
                    
                    // Send success notification via SignalR
                    var menuResponseDto = completeMenu.ToResponse();
                    await _hubContext.Clients.Group($"menu-{menuId}")
                        .SendAsync("MenuGenerated", menuResponseDto);
                    
                    _logger.LogInformation("Menu generation completed and notification sent. MenuId: {MenuId}", menuId);
                }
                else
                {
                    throw new InvalidOperationException("Failed to load generated menu");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during menu generation for MenuId: {MenuId}", menuId);
                
                // Clean up temporary menu record on failure
                try
                {
                    using var cleanupScope = _scopeFactory.CreateScope();
                    var cleanupContext = cleanupScope.ServiceProvider.GetRequiredService<CuisinierDbContext>();
                    
                    var failedMenu = await cleanupContext.Menus
                        .Include(m => m.Recipes)
                        .FirstOrDefaultAsync(m => m.Id == menuId);
                    
                    if (failedMenu != null)
                    {
                        // Only delete if the menu has no recipes (transaction was rolled back)
                        // This ensures we don't accidentally delete a partially completed menu
                        if (!failedMenu.Recipes.Any())
                        {
                            cleanupContext.Menus.Remove(failedMenu);
                            
                            try
                            {
                                await cleanupContext.SaveChangesAsync();
                                _logger.LogInformation(
                                    "Cleaned up temporary menu after generation failure. MenuId: {MenuId}",
                                    menuId);
                            }
                            catch (DbUpdateConcurrencyException concurrencyEx)
                            {
                                _logger.LogWarning(
                                    concurrencyEx,
                                    "Menu was already deleted during cleanup. MenuId: {MenuId}",
                                    menuId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Menu has recipes despite failure, not cleaning up. MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                                menuId,
                                failedMenu.Recipes.Count);
                        }
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(
                        cleanupEx,
                        "Failed to clean up temporary menu after generation failure. MenuId: {MenuId}",
                        menuId);
                }
                
                // Send error notification
                try
                {
                    await _hubContext.Clients.Group($"menu-{menuId}")
                        .SendAsync("MenuGenerationError", menuId);
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(
                        notificationEx,
                        "Failed to send error notification for menu generation failure. MenuId: {MenuId}",
                        menuId);
                }
            }
        });

        return Task.CompletedTask;
    }

    private class IngredientEqualityComparer : IEqualityComparer<(string Name, string Quantity)>
    {
        public bool Equals((string Name, string Quantity) x, (string Name, string Quantity) y)
        {
            return x.Name == y.Name && x.Quantity == y.Quantity;
        }

        public int GetHashCode((string Name, string Quantity) obj)
        {
            return HashCode.Combine(obj.Name, obj.Quantity);
        }
    }
}
