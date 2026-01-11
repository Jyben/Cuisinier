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

    public void StartGenerateMenuInBackground(int menuId, MenuGenerationRequest request, string userId)
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

                        // Get menu (it should already exist with the provided ID and userId)
                        var menu = await context.Menus
                            .Include(m => m.Recipes)
                            .FirstOrDefaultAsync(m => m.Id == menuId && m.UserId == userId);

                        if (menu == null)
                        {
                            throw new InvalidOperationException($"Menu {menuId} not found for user {userId}. It should have been created before starting background generation.");
                        }

                        // If existing recipes are provided, reuse them (must belong to a menu owned by this user)
                        if (request.RecipeIds != null && request.RecipeIds.Any())
                        {
                            var existingRecipes = await context.Recipes
                                .Include(r => r.Ingredients)
                                .Include(r => r.Menu)
                                .Where(r => request.RecipeIds.Contains(r.Id) && r.Menu != null && r.Menu.UserId == userId)
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
                        // Check for duplicates in dishes and favorites before adding
                        // Dishes are shared (not user-specific), so we don't filter by UserId
                        var allDishes = await context.Dishes
                            .Include(d => d.Ingredients)
                            .ToListAsync();
                        var allFavorites = await context.Favorites
                            .Include(f => f.Ingredients)
                            .Where(f => f.UserId == userId)
                            .ToListAsync();

                        foreach (var recipeResponse in menuResponse.Recipes)
                        {
                            var recipe = recipeResponse.ToEntity(menu.Id);
                            
                            // Check if this recipe already exists in dishes first
                            var normalizedTitle = recipe.Title.Trim().ToLower();
                            var matchingDishes = allDishes
                                .Where(d => d.Title.Trim().ToLower() == normalizedTitle)
                                .ToList();

                            bool foundMatch = false;
                            foreach (var dish in matchingDishes)
                            {
                                // Check if ingredients match
                                if (dish.Ingredients.Count != recipe.Ingredients.Count)
                                    continue;

                                var dishIngredients = dish.Ingredients
                                    .Select(i => (Name: i.Name.Trim().ToLower(), Quantity: i.Quantity.Trim().ToLower()))
                                    .OrderBy(i => i.Name)
                                    .ThenBy(i => i.Quantity)
                                    .ToList();

                                var recipeIngredients = recipe.Ingredients
                                    .Select(i => (Name: i.Name.Trim().ToLower(), Quantity: i.Quantity.Trim().ToLower()))
                                    .OrderBy(i => i.Name)
                                    .ThenBy(i => i.Quantity)
                                    .ToList();

                                if (dishIngredients.SequenceEqual(recipeIngredients, new IngredientEqualityComparer()))
                                {
                                    // Link to dish but don't change IsFromDatabase
                                    // IsFromDatabase should only be true if recipe actually came from database
                                    recipe.OriginalDishId = dish.Id;
                                    recipe.DishId = dish.Id;
                                    _logger.LogInformation(
                                        "Recipe '{Title}' matches existing dish (ID: {DishId})",
                                        recipe.Title,
                                        dish.Id);
                                    foundMatch = true;
                                    break;
                                }
                            }

                            // If not found in dishes, check favorites
                            if (!foundMatch)
                            {
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
                                        foundMatch = true;
                                        break;
                                    }
                                }
                            }

                            // If no match found, create a new Dish for this recipe
                            if (!foundMatch)
                            {
                                var newDish = new Dish
                                {
                                    UserId = null, // Dishes are shared (not user-specific)
                                    Title = recipe.Title,
                                    Description = recipe.Description,
                                    CompleteDescription = recipe.CompleteDescription,
                                    DetailedRecipe = recipe.DetailedRecipe,
                                    ImageUrl = recipe.ImageUrl,
                                    PreparationTime = recipe.PreparationTime,
                                    CookingTime = recipe.CookingTime,
                                    Kcal = recipe.Kcal,
                                    Servings = recipe.Servings,
                                    CreatedAt = DateTime.UtcNow,
                                    Ingredients = recipe.Ingredients.Select(i => new DishIngredient
                                    {
                                        Name = i.Name,
                                        Quantity = i.Quantity,
                                        Category = i.Category
                                    }).ToList()
                                };

                                context.Dishes.Add(newDish);
                                await context.SaveChangesAsync();

                                // Link recipe to the new dish
                                // Don't change IsFromDatabase - keep it as false for LLM-generated recipes
                                recipe.DishId = newDish.Id;
                                recipe.OriginalDishId = newDish.Id;

                                _logger.LogInformation(
                                    "Created new dish for recipe '{Title}' (DishId: {DishId})",
                                    recipe.Title,
                                    newDish.Id);
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
                    .FirstOrDefaultAsync(m => m.Id == menuId && m.UserId == userId);

                if (completeMenu != null)
                {
                    // Invalidate cache
                    _cache.Remove($"{MenuCacheKeyPrefix}{userId}_{menuId}");
                    _cache.Remove($"{AllMenusCacheKey}_{userId}");
                    
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
                    
                    // Check if menu has any recipes before loading the full menu
                    var hasRecipes = await cleanupContext.Recipes.AnyAsync(r => r.MenuId == menuId);
                    
                    if (!hasRecipes)
                    {
                        var failedMenu = await cleanupContext.Menus.FindAsync(menuId);
                        
                        if (failedMenu != null)
                        {
                            cleanupContext.Menus.Remove(failedMenu);
                            
                            try
                            {
                                await cleanupContext.SaveChangesAsync();
                                _logger.LogInformation(
                                    "Cleaned up temporary menu after generation failure. MenuId: {MenuId}",
                                    menuId);
                            }
                            catch (Exception saveEx)
                            {
                                _logger.LogWarning(
                                    saveEx,
                                    "Failed to save menu deletion (possibly already deleted). MenuId: {MenuId}",
                                    menuId);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Menu has recipes despite failure, not cleaning up. MenuId: {MenuId}",
                            menuId);
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
