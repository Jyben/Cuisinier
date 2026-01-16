using System.Text.Json;
using Cuisinier.Shared.DTOs;
using Cuisinier.Shared.Helpers;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Mappings;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cuisinier.Api.Services;

public class MenuService : IMenuService
{
    private readonly CuisinierDbContext _context;
    private readonly IOpenAIService _openAIService;
    private readonly BackgroundRecipeService _backgroundRecipeService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MenuService> _logger;
    private readonly IUserAccessService _userAccessService;

    private const string LastParametersCacheKey = "Menu_LastParameters";
    private const string MenuCacheKeyPrefix = "Menu_";
    private const string AllMenusCacheKey = "Menu_All";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MenuCacheExpiration = TimeSpan.FromMinutes(10);

    public MenuService(
        CuisinierDbContext context,
        IOpenAIService openAIService,
        BackgroundRecipeService backgroundRecipeService,
        IMemoryCache cache,
        ILogger<MenuService> logger,
        IUserAccessService userAccessService)
    {
        _context = context;
        _openAIService = openAIService;
        _backgroundRecipeService = backgroundRecipeService;
        _cache = cache;
        _logger = logger;
        _userAccessService = userAccessService;
    }

    public async Task<int> StartMenuGenerationAsync(MenuGenerationRequest request, string userId)
    {
        _logger.LogInformation(
            "Starting menu generation. WeekStartDate: {WeekStartDate}, RecipeIdsCount: {RecipeIdsCount}",
            request.Parameters.WeekStartDate,
            request.RecipeIds?.Count ?? 0);

        // Use execution strategy to wrap transaction (required when EnableRetryOnFailure is enabled)
        var strategy = _context.Database.CreateExecutionStrategy();
        int menuId = 0;
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete all non-validated menus (menus without shopping list) before generating a new one (for this user)
                var nonValidatedMenus = await _context.Menus
                    .Where(m => m.UserId == userId && !_context.ShoppingLists.Any(sl => sl.MenuId == m.Id))
                    .ToListAsync();

                if (nonValidatedMenus.Any())
                {
                    _logger.LogInformation(
                        "Deleting {Count} non-validated menus before generating new menu. UserId: {UserId}",
                        nonValidatedMenus.Count,
                        userId);
                    _context.Menus.RemoveRange(nonValidatedMenus);
                    await _context.SaveChangesAsync();
                }

                // Save parameters in MenuSettings entity (independent of the menu)
                await SaveParametersInternalAsync(request.Parameters, userId);

                // Create a temporary menu (will be populated by background service)
                var menu = new Menu
                {
                    UserId = userId,
                    WeekStartDate = request.Parameters.WeekStartDate,
                    CreationDate = DateTime.UtcNow,
                    GenerationParametersJson = null
                };

                _context.Menus.Add(menu);
                await _context.SaveChangesAsync();

                // Capture the ID before commit
                menuId = menu.Id;

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Temporary menu created. MenuId: {MenuId}",
                    menu.Id);
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(
                        rollbackEx,
                        "An error occurred while rolling back the transaction in StartMenuGenerationAsync.");
                }

                _logger.LogError(
                    ex,
                    "An error occurred during the menu generation transaction in StartMenuGenerationAsync.");
                throw;
            }
        });

        if (menuId == 0)
        {
            throw new InvalidOperationException("Failed to create menu in transaction");
        }

        return menuId;
    }

    public async Task<MenuResponse> GenerateMenuAsync(MenuGenerationRequest request, string userId)
    {
        _logger.LogInformation(
            "Generating menu. WeekStartDate: {WeekStartDate}, RecipeIdsCount: {RecipeIdsCount}",
            request.Parameters.WeekStartDate,
            request.RecipeIds?.Count ?? 0);

        // Generate menu from OpenAI (outside transaction as it's an external call)
        var menuResponse = await _openAIService.GenerateMenuAsync(request.Parameters);

        // Use execution strategy to wrap transaction (required when EnableRetryOnFailure is enabled)
        var strategy = _context.Database.CreateExecutionStrategy();
        int menuId = 0;
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Delete all non-validated menus (menus without shopping list) before generating a new one (for this user)
                var nonValidatedMenus = await _context.Menus
                    .Where(m => m.UserId == userId && !_context.ShoppingLists.Any(sl => sl.MenuId == m.Id))
                    .ToListAsync();

                if (nonValidatedMenus.Any())
                {
                    _logger.LogInformation(
                        "Deleting {Count} non-validated menus before generating new menu. UserId: {UserId}",
                        nonValidatedMenus.Count,
                        userId);
                    _context.Menus.RemoveRange(nonValidatedMenus);
                    await _context.SaveChangesAsync();
                }

                // Save parameters in MenuSettings entity (independent of the menu)
                await SaveParametersInternalAsync(request.Parameters, userId);

                // Create menu in database (without parameters, they are now in MenuSettings)
                var menu = new Menu
                {
                    UserId = userId,
                    WeekStartDate = request.Parameters.WeekStartDate,
                    CreationDate = DateTime.UtcNow,
                    GenerationParametersJson = null
                };

                // If existing recipes are provided, reuse them (must belong to a menu owned by this user)
                if (request.RecipeIds != null && request.RecipeIds.Any())
                {
                    var existingRecipes = await _context.Recipes
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
                // Check for duplicates in dishes and favorites before adding (for this user)
                // Dishes are shared (not user-specific), so we don't filter by UserId
                var allDishes = await _context.Dishes
                    .Include(d => d.Ingredients)
                    .ToListAsync();
                var allFavorites = await _context.Favorites
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
                            // Mark as from database and link to dish
                            recipe.IsFromDatabase = true;
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
                                // Mark as from database and link to favorite (using OriginalDishId for backward compatibility)
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

                        _context.Dishes.Add(newDish);
                        await _context.SaveChangesAsync();

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

                _context.Menus.Add(menu);
                await _context.SaveChangesAsync();

                // Capture the ID before commit
                menuId = menu.Id;

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

        if (menuId == 0)
        {
            throw new InvalidOperationException("Failed to create menu in transaction");
        }

        // Load with relationships for response (outside transaction)
        var completeMenu = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        return completeMenu?.ToResponse() 
            ?? throw new InvalidOperationException("Failed to load generated menu");
    }

    public async Task<MenuResponse?> GetMenuAsync(int id, string userId)
    {
        var cacheKey = $"{MenuCacheKeyPrefix}{userId}_{id}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out MenuResponse? cachedMenu))
        {
            _logger.LogInformation("Retrieved menu from cache. MenuId: {MenuId}, UserId: {UserId}", id, userId);
            return cachedMenu;
        }

        _logger.LogInformation("Retrieving menu from database. MenuId: {MenuId}, UserId: {UserId}", id, userId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        var menu = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == id && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            _logger.LogWarning("Menu not found. MenuId: {MenuId}", id);
            return null;
        }

        var menuResponse = menu.ToResponse();

        // Cache the result (only cache validated menus to avoid caching temporary data)
        var isValidated = await _context.ShoppingLists.AnyAsync(sl => sl.MenuId == id && accessibleUserIds.Contains(sl.UserId));
        if (isValidated)
        {
            _cache.Set(cacheKey, menuResponse, MenuCacheExpiration);
            _logger.LogInformation(
                "Menu retrieved and cached. MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                menu.Id,
                menu.Recipes.Count);
        }
        else
        {
            _logger.LogInformation(
                "Menu retrieved (not cached, not validated). MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                menu.Id,
                menu.Recipes.Count);
        }

        return menuResponse;
    }

    public async Task<List<MenuResponse>> GetAllMenusAsync(string userId)
    {
        var cacheKey = $"{AllMenusCacheKey}_{userId}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<MenuResponse>? cachedMenus))
        {
            _logger.LogInformation("Retrieved all menus from cache. Count: {Count}, UserId: {UserId}", cachedMenus?.Count ?? 0, userId);
            return cachedMenus ?? new List<MenuResponse>();
        }

        _logger.LogInformation("Retrieving all menus from database. UserId: {UserId}", userId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        // Only return validated menus (menus that have a shopping list) for accessible users
        var menus = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .Where(m => accessibleUserIds.Contains(m.UserId) && _context.ShoppingLists.Any(sl => sl.MenuId == m.Id && accessibleUserIds.Contains(sl.UserId)))
            .OrderByDescending(m => m.WeekStartDate)
            .ToListAsync();

        var menuResponses = menus.Select(m => m.ToResponse()).ToList();

        // Cache the result
        _cache.Set(cacheKey, menuResponses, MenuCacheExpiration);

        _logger.LogInformation("All menus retrieved and cached. Count: {Count}, UserId: {UserId}", menuResponses.Count, userId);

        return menuResponses;
    }

    public async Task<MenuParameters?> GetLastParametersAsync(string userId)
    {
        var cacheKey = $"{LastParametersCacheKey}_{userId}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out MenuParameters? cachedParameters))
        {
            _logger.LogInformation("Retrieved last menu parameters from cache. UserId: {UserId}", userId);
            return cachedParameters;
        }

        // Get parameters from MenuSettings entity for this user
        var menuSettings = await _context.MenuSettings
            .FirstOrDefaultAsync(ms => ms.UserId == userId);

        if (menuSettings == null || string.IsNullOrEmpty(menuSettings.ParametersJson))
        {
            return null;
        }

        try
        {
            var jsonOptions = JsonOptionsHelper.GetDefaultOptions();
            var parameters = JsonSerializer.Deserialize<MenuParameters>(menuSettings.ParametersJson, jsonOptions);

            // Migrate from legacy format if needed
            if (parameters != null)
            {
                parameters = MenuParametersMigrationHelper.MigrateIfNeeded(parameters);

                if (parameters.IsLegacyFormat)
                {
                    _logger.LogInformation("Migrated legacy menu parameters to new format. UserId: {UserId}", userId);
                }
            }

            // Cache the result
            _cache.Set(cacheKey, parameters, CacheExpiration);

            _logger.LogInformation("Retrieved last menu parameters successfully from database and cached. UserId: {UserId}", userId);
            return parameters;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialize menu parameters from database. ParametersJson length: {Length}",
                menuSettings.ParametersJson?.Length ?? 0);
            return null;
        }
    }

    public async Task SaveParametersAsync(MenuParameters parameters, string userId)
    {
        await SaveParametersInternalAsync(parameters, userId);
    }

    private async Task SaveParametersInternalAsync(MenuParameters parameters, string userId)
    {
        var jsonOptions = JsonOptionsHelper.GetSerializationOptions();
        
        // Create a complete deep copy via serialization/deserialization to avoid any reference issues
        var parametersJson = JsonSerializer.Serialize(parameters, jsonOptions);
        var parametersForSave = JsonSerializer.Deserialize<MenuParameters>(parametersJson, jsonOptions) 
            ?? new MenuParameters();
        
        // Reset the date so it's not saved (it will be recalculated each time)
        parametersForSave.WeekStartDate = default;

        // Get or create MenuSettings record for this user
        var menuSettings = await _context.MenuSettings
            .FirstOrDefaultAsync(ms => ms.UserId == userId);
        
        if (menuSettings == null)
        {
            menuSettings = new MenuSettings
            {
                UserId = userId,
                ParametersJson = JsonSerializer.Serialize(parametersForSave, jsonOptions),
                ModificationDate = DateTime.UtcNow
            };
            _context.MenuSettings.Add(menuSettings);
        }
        else
        {
            menuSettings.ParametersJson = JsonSerializer.Serialize(parametersForSave, jsonOptions);
            menuSettings.ModificationDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        
        // Invalidate cache when parameters are saved (for this user)
        _cache.Remove($"{LastParametersCacheKey}_{userId}");
        _logger.LogInformation("Menu parameters saved and cache invalidated. UserId: {UserId}", userId);
    }

    public async Task<RecipeResponse> ReplaceRecipeAsync(int menuId, int recipeId, RecipeReplacementRequest request, string userId)
    {
        _logger.LogInformation(
            "Replacing recipe. MenuId: {MenuId}, RecipeId: {RecipeId}",
            menuId,
            recipeId);

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.MenuId == menuId);

        if (recipe == null)
        {
            throw new KeyNotFoundException($"Recipe {recipeId} not found in menu {menuId}");
        }

        var recipeResponse = recipe.ToResponse();
        
        // Preserve original servings count
        var originalServings = recipe.Servings;
        
        var newRecipeResponse = await _openAIService.ReplaceRecipeAsync(request.Parameters, recipeResponse);

        // Use execution strategy to wrap transaction (required when EnableRetryOnFailure is enabled)
        var strategy = _context.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Remove old ingredients (they will be replaced)
                _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
                await _context.SaveChangesAsync();

                // Update the existing recipe with new data (keeping the same ID to preserve order)
                recipe.Title = newRecipeResponse.Title;
                recipe.Description = newRecipeResponse.Description;
                recipe.CompleteDescription = newRecipeResponse.CompleteDescription;
                recipe.DetailedRecipe = newRecipeResponse.DetailedRecipe;
                recipe.ImageUrl = newRecipeResponse.ImageUrl;
                recipe.PreparationTime = newRecipeResponse.PreparationTime;
                recipe.CookingTime = newRecipeResponse.CookingTime;
                recipe.Kcal = newRecipeResponse.Kcal;
                recipe.Servings = originalServings; // Preserve original servings count
                recipe.IsFromDatabase = newRecipeResponse.IsFromDatabase;
                recipe.OriginalDishId = newRecipeResponse.OriginalDishId;

                // Add new ingredients
                recipe.Ingredients = newRecipeResponse.Ingredients.Select(i => new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Category = i.Category
                }).ToList();

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        // Reload with relationships for response (outside transaction)
        var completeRecipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        // Invalidate cache
        _cache.Remove($"{MenuCacheKeyPrefix}{userId}_{menuId}");
        _cache.Remove($"{AllMenusCacheKey}_{userId}");
        _cache.Remove($"Recipe_{recipeId}");

        _logger.LogInformation(
            "Recipe replaced successfully and cache invalidated. MenuId: {MenuId}, RecipeId: {RecipeId}, UserId: {UserId}",
            menuId,
            recipeId,
            userId);

        return completeRecipe?.ToResponse() 
            ?? throw new InvalidOperationException("Failed to load replaced recipe");
    }

    public async Task<RecipeResponse> ReplaceIngredientAsync(int menuId, int recipeId, IngredientReplacementRequest request, string userId)
    {
        _logger.LogInformation(
            "Replacing ingredient. MenuId: {MenuId}, RecipeId: {RecipeId}, IngredientToReplace: {IngredientToReplace}, NewIngredient: {NewIngredient}",
            menuId,
            recipeId,
            request.IngredientToReplace,
            request.NewIngredient);

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.MenuId == menuId);

        if (recipe == null)
        {
            throw new KeyNotFoundException($"Recipe {recipeId} not found in menu {menuId}");
        }

        var ingredient = recipe.Ingredients.FirstOrDefault(
            i => i.Name.Equals(request.IngredientToReplace, StringComparison.OrdinalIgnoreCase));
        
        if (ingredient != null)
        {
            ingredient.Name = request.NewIngredient;
        }
        else
        {
            _logger.LogWarning(
                "Ingredient not found for replacement. MenuId: {MenuId}, RecipeId: {RecipeId}, IngredientToReplace: {IngredientToReplace}",
                menuId,
                recipeId,
                request.IngredientToReplace);
        }

        await _context.SaveChangesAsync();

        var completeRecipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId);

        // Invalidate cache
        _cache.Remove($"{MenuCacheKeyPrefix}{userId}_{menuId}");
        _cache.Remove($"Recipe_{recipeId}");

        return completeRecipe?.ToResponse() 
            ?? throw new InvalidOperationException("Failed to load recipe after ingredient replacement");
    }

    public async Task<RecipeResponse> AddFavoriteToMenuAsync(int menuId, int favoriteId, string userId)
    {
        _logger.LogInformation("Adding favorite to menu. MenuId: {MenuId}, FavoriteId: {FavoriteId}", menuId, favoriteId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found for user {userId}");
        }

        // Favorites are user-specific, but we allow adding favorites from linked users
        var favorite = await _context.Favorites
            .Include(f => f.Ingredients)
            .FirstOrDefaultAsync(f => f.Id == favoriteId && accessibleUserIds.Contains(f.UserId));

        if (favorite == null)
        {
            throw new KeyNotFoundException($"Favorite {favoriteId} not found for user {userId}");
        }

        // Convert favorite to recipe
        var recipe = new Recipe
        {
            MenuId = menuId,
            Title = favorite.Title,
            Description = favorite.Description,
            CompleteDescription = favorite.CompleteDescription,
            DetailedRecipe = favorite.DetailedRecipe,
            ImageUrl = favorite.ImageUrl,
            PreparationTime = favorite.PreparationTime,
            CookingTime = favorite.CookingTime,
            Kcal = favorite.Kcal,
            Servings = favorite.Servings,
            IsFromDatabase = true,
            OriginalDishId = null, // OriginalDishId references Recipes table, not Favorites
            Ingredients = favorite.Ingredients.Select(i => new RecipeIngredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };

        _context.Recipes.Add(recipe);
        await _context.SaveChangesAsync();

        // Load with ingredients for response
        await _context.Entry(recipe)
            .Collection(r => r.Ingredients)
            .LoadAsync();

        // Update shopping list if it exists (menu already validated)
        var existingShoppingList = await _context.ShoppingLists
            .Include(sl => sl.Items)
            .FirstOrDefaultAsync(sl => sl.MenuId == menuId && accessibleUserIds.Contains(sl.UserId));

        if (existingShoppingList != null)
        {
            // Add ingredients from favorite recipe to shopping list
            foreach (var ingredient in recipe.Ingredients)
            {
                var shoppingListItem = new ShoppingListItem
                {
                    ShoppingListId = existingShoppingList.Id,
                    Name = ingredient.Name,
                    Quantity = ingredient.Quantity,
                    Category = ingredient.Category,
                    IsManuallyAdded = false
                };

                _context.ShoppingListItems.Add(shoppingListItem);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Shopping list updated with favorite ingredients. MenuId: {MenuId}, ItemsAdded: {ItemsCount}",
                menuId,
                recipe.Ingredients.Count);
        }

        // Invalidate cache for all accessible users
        foreach (var uid in accessibleUserIds)
        {
            _cache.Remove($"{MenuCacheKeyPrefix}{uid}_{menuId}");
            _cache.Remove($"{AllMenusCacheKey}_{uid}");
        }

        _logger.LogInformation("Favorite added to menu successfully. MenuId: {MenuId}, RecipeId: {RecipeId}, UserId: {UserId}", menuId, recipe.Id, userId);

        return recipe.ToResponse();
    }

    public async Task DeleteRecipeAsync(int menuId, int recipeId, string userId)
    {
        _logger.LogInformation(
            "Deleting recipe. MenuId: {MenuId}, RecipeId: {RecipeId}",
            menuId,
            recipeId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        // Verify menu belongs to user or linked family user
        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found for user {userId}");
        }

        // Load recipe with ingredients before deletion
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.MenuId == menuId);

        if (recipe == null)
        {
            throw new KeyNotFoundException($"Recipe {recipeId} not found in menu {menuId}");
        }

        // Store ingredients before deletion
        var recipeIngredients = recipe.Ingredients.ToList();

        // Remove recipe
        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();

        // Remove ingredients from shopping list if it exists
        var shoppingList = await _context.ShoppingLists
            .Include(sl => sl.Items)
            .FirstOrDefaultAsync(sl => sl.MenuId == menuId && accessibleUserIds.Contains(sl.UserId));

        if (shoppingList != null && recipeIngredients.Any())
        {
            var itemsRemoved = 0;

            // For each ingredient in the deleted recipe, try to find and remove matching items
            // This works best for favorites added individually, but may be less precise for LLM-grouped ingredients
            foreach (var ingredient in recipeIngredients)
            {
                // Find items that match the ingredient (name and category, not manually added)
                // Use case-insensitive comparison for name
                var matchingItems = shoppingList.Items
                    .Where(item => 
                        !item.IsManuallyAdded &&
                        item.Name.Trim().Equals(ingredient.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        item.Category.Trim().Equals(ingredient.Category.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var item in matchingItems)
                {
                    _context.ShoppingListItems.Remove(item);
                    itemsRemoved++;
                }
            }

            if (itemsRemoved > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Removed {ItemsCount} items from shopping list after recipe deletion. MenuId: {MenuId}, RecipeId: {RecipeId}",
                    itemsRemoved,
                    menuId,
                    recipeId);
            }
        }

        // Invalidate cache for all accessible users
        foreach (var uid in accessibleUserIds)
        {
            _cache.Remove($"{MenuCacheKeyPrefix}{uid}_{menuId}");
            _cache.Remove($"{AllMenusCacheKey}_{uid}");
        }
        _cache.Remove($"Recipe_{recipeId}");

        _logger.LogInformation(
            "Recipe deleted successfully and cache invalidated. MenuId: {MenuId}, RecipeId: {RecipeId}, UserId: {UserId}",
            menuId,
            recipeId,
            userId);
    }

    public async Task<RecipeResponse> ToggleRecipeCookedStatusAsync(int menuId, int recipeId, string userId)
    {
        _logger.LogInformation(
            "Toggling cooked status. MenuId: {MenuId}, RecipeId: {RecipeId}",
            menuId,
            recipeId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        // Verify menu belongs to user or linked family user
        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found for user {userId}");
        }

        // Load recipe
        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.MenuId == menuId);

        if (recipe == null)
        {
            throw new KeyNotFoundException($"Recipe {recipeId} not found in menu {menuId}");
        }

        // Toggle cooked status
        recipe.IsCooked = !recipe.IsCooked;
        await _context.SaveChangesAsync();

        // Invalidate cache for all accessible users
        foreach (var uid in accessibleUserIds)
        {
            _cache.Remove($"{MenuCacheKeyPrefix}{uid}_{menuId}");
            _cache.Remove($"{AllMenusCacheKey}_{uid}");
        }

        _logger.LogInformation(
            "Recipe cooked status toggled successfully. MenuId: {MenuId}, RecipeId: {RecipeId}, IsCooked: {IsCooked}, UserId: {UserId}",
            menuId,
            recipeId,
            recipe.IsCooked,
            userId);

        return recipe.ToResponse();
    }

    public async Task DeleteMenuAsync(int menuId, string userId)
    {
        _logger.LogInformation("Deleting menu. MenuId: {MenuId}", menuId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found for user {userId}");
        }

        // Delete associated shopping lists first (to avoid FK constraint violation)
        var shoppingLists = await _context.ShoppingLists
            .Where(sl => sl.MenuId == menuId)
            .ToListAsync();

        if (shoppingLists.Count != 0)
        {
            _logger.LogInformation("Deleting {Count} associated shopping list(s) for menu {MenuId}", shoppingLists.Count, menuId);
            _context.ShoppingLists.RemoveRange(shoppingLists);
        }

        _context.Menus.Remove(menu);
        await _context.SaveChangesAsync();

        // Invalidate cache for all accessible users
        foreach (var uid in accessibleUserIds)
        {
            _cache.Remove($"{MenuCacheKeyPrefix}{uid}_{menuId}");
            _cache.Remove($"{AllMenusCacheKey}_{uid}");
        }

        _logger.LogInformation("Menu deleted successfully and cache invalidated. MenuId: {MenuId}, UserId: {UserId}", menuId, userId);
    }

    public async Task<MenuResponse> ValidateMenuAsync(int menuId, ValidateMenuRequest? request, string userId)
    {
        _logger.LogInformation("Validating menu. MenuId: {MenuId}", menuId);

        // Get accessible user IDs (includes linked family user if any)
        var accessibleUserIds = await _userAccessService.GetAccessibleUserIdsAsync(userId);

        var menu = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found for user {userId}");
        }

        // Check for duplicates in LLM-generated recipes before validation
        var llmRecipesToCheck = menu.Recipes.Where(r => !r.IsFromDatabase).ToList();
        if (llmRecipesToCheck.Any())
        {
            _logger.LogInformation(
                "Checking {Count} LLM-generated recipes for duplicates in favorites. MenuId: {MenuId}, UserId: {UserId}",
                llmRecipesToCheck.Count,
                menuId,
                userId);

            var duplicatesFound = await CheckAndMarkDuplicatesAsync(llmRecipesToCheck, userId);
            
            if (duplicatesFound > 0)
            {
                await _context.SaveChangesAsync();
                
                // Reload menu to get updated recipes
                menu = await _context.Menus
                    .Include(m => m.Recipes)
                        .ThenInclude(r => r.Ingredients)
                    .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

                if (menu == null)
                {
                    throw new InvalidOperationException("Failed to reload menu after marking duplicates");
                }

                _logger.LogInformation(
                    "Marked {Count} LLM recipes as favorites (duplicates found). MenuId: {MenuId}",
                    duplicatesFound,
                    menuId);
            }
        }

        // Add favorites if provided (filtered by accessible users)
        if (request?.FavoriteIds != null && request.FavoriteIds.Any())
        {
            var favorites = await _context.Favorites
                .Include(f => f.Ingredients)
                .Where(f => request.FavoriteIds.Contains(f.Id) && accessibleUserIds.Contains(f.UserId))
                .ToListAsync();

            foreach (var favorite in favorites)
            {
                var recipe = new Recipe
                {
                    MenuId = menuId,
                    Title = favorite.Title,
                    Description = favorite.Description,
                    CompleteDescription = favorite.CompleteDescription,
                    DetailedRecipe = favorite.DetailedRecipe,
                    ImageUrl = favorite.ImageUrl,
                    PreparationTime = favorite.PreparationTime,
                    CookingTime = favorite.CookingTime,
                    Kcal = favorite.Kcal,
                    Servings = favorite.Servings,
                    IsFromDatabase = true,
                    OriginalDishId = null, // OriginalDishId references Recipes table, not Favorites
                    Ingredients = favorite.Ingredients.Select(i => new RecipeIngredient
                    {
                        Name = i.Name,
                        Quantity = i.Quantity,
                        Category = i.Category
                    }).ToList()
                };

                menu.Recipes.Add(recipe);
            }

            await _context.SaveChangesAsync();

            // Reload menu with new recipes
            menu = await _context.Menus
                .Include(m => m.Recipes)
                    .ThenInclude(r => r.Ingredients)
                .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

            if (menu == null)
            {
                throw new InvalidOperationException("Failed to reload menu after adding favorites");
            }

            _logger.LogInformation(
                "Added {Count} favorites to menu. MenuId: {MenuId}",
                favorites.Count,
                menuId);
        }

        // Ensure each recipe is linked to a Dish entity (so it appears in /dishes list).
        // Done at validation time to also cover background generation flows and avoid persisting dishes for non-validated menus.
        await EnsureDishesForMenuAsync(menu, userId);

            // Check if shopping list already exists
        var existingShoppingList = await _context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId && accessibleUserIds.Contains(l.UserId));

        if (existingShoppingList == null)
        {
            // Separate recipes: favorites (from database) and LLM-generated recipes
            var favoriteRecipes = menu.Recipes.Where(r => r.IsFromDatabase).ToList();
            var llmRecipes = menu.Recipes.Where(r => !r.IsFromDatabase).ToList();

            // Generate shopping list from LLM only for non-favorite recipes
            var shoppingListItems = new List<ShoppingListItem>();
            
            if (llmRecipes.Any())
            {
                var llmRecipesResponse = llmRecipes.Select(r => r.ToResponse()).ToList();
                var shoppingListResponse = await _openAIService.GenerateShoppingListAsync(llmRecipesResponse);
                
                shoppingListItems.AddRange(shoppingListResponse.Items.Select(i => new ShoppingListItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Category = i.Category,
                    IsManuallyAdded = i.IsManuallyAdded
                }));
            }

            // Add ingredients from favorite recipes manually (not via LLM)
            foreach (var favoriteRecipe in favoriteRecipes)
            {
                foreach (var ingredient in favoriteRecipe.Ingredients)
                {
                    shoppingListItems.Add(new ShoppingListItem
                    {
                        Name = ingredient.Name,
                        Quantity = ingredient.Quantity,
                        Category = ingredient.Category,
                        IsManuallyAdded = false
                    });
                }
            }

            var shoppingList = new ShoppingList
            {
                UserId = userId,
                MenuId = menuId,
                CreationDate = DateTime.UtcNow,
                Items = shoppingListItems
            };

            _context.ShoppingLists.Add(shoppingList);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Shopping list generated for menu. MenuId: {MenuId}, ItemsCount: {ItemsCount}",
                menuId,
                shoppingList.Items.Count);

            // Invalidate cache when menu is validated (it becomes a validated menu) for all accessible users
            foreach (var uid in accessibleUserIds)
            {
                _cache.Remove($"{MenuCacheKeyPrefix}{uid}_{menuId}");
                _cache.Remove($"{AllMenusCacheKey}_{uid}");
            }

            // Launch detailed recipe generation in background (only for non-favorite recipes without detail)
            var totalRecipes = menu.Recipes.Count;
            var fromDatabaseCount = menu.Recipes.Count(r => r.IsFromDatabase);
            var withDetailedRecipe = menu.Recipes.Count(r => !string.IsNullOrEmpty(r.DetailedRecipe));
            
            _logger.LogInformation(
                "Menu {MenuId}: Total recipes: {Total}, FromDatabase: {FromDb}, WithDetailedRecipe: {WithDetail}",
                menuId, totalRecipes, fromDatabaseCount, withDetailedRecipe);
            
            var recipesWithoutDetail = menu.Recipes
                .Where(r => !r.IsFromDatabase && string.IsNullOrEmpty(r.DetailedRecipe))
                .Select(r => r.Id)
                .ToList();

            _logger.LogInformation(
                "Menu {MenuId}: Recipes without detail (LLM-generated): {Count}",
                menuId,
                recipesWithoutDetail.Count);

            if (recipesWithoutDetail.Any())
            {
                _logger.LogInformation(
                    "Launching background detailed recipe generation. MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                    menuId,
                    recipesWithoutDetail.Count);
                await _backgroundRecipeService.GenerateDetailedRecipesAsync(menuId, recipesWithoutDetail);
            }
            else
            {
                _logger.LogInformation(
                    "No recipes to generate detailed recipes for. MenuId: {MenuId}",
                    menuId);
            }
        }
        else
        {
            _logger.LogInformation(
                "Menu already validated. Shopping list exists. MenuId: {MenuId}",
                menuId);
        }

        _logger.LogInformation("Menu validated successfully. MenuId: {MenuId}", menuId);

        return menu.ToResponse();
    }

    private async Task EnsureDishesForMenuAsync(Menu menu, string userId)
    {
        // Dishes are shared (not user-specific), so we don't filter by UserId
        var allDishes = await _context.Dishes
            .Include(d => d.Ingredients)
            .ToListAsync();

        var anyChanges = false;

        foreach (var recipe in menu.Recipes)
        {
            if (recipe.DishId.HasValue || recipe.Dish != null)
                continue;

            var normalizedTitle = recipe.Title.Trim().ToLower();
            var matchingDishes = allDishes
                .Where(d => d.Title.Trim().ToLower() == normalizedTitle)
                .ToList();

            Dish? matchedDish = null;
            foreach (var dish in matchingDishes)
            {
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
                    matchedDish = dish;
                    break;
                }
            }

            if (matchedDish != null)
            {
                recipe.DishId = matchedDish.Id;
                recipe.OriginalDishId ??= matchedDish.Id;
                // Don't change IsFromDatabase here - only link to existing dish
                // IsFromDatabase should only be true if recipe actually came from database
                anyChanges = true;
                continue;
            }

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

            _context.Dishes.Add(newDish);
            allDishes.Add(newDish);

            // Link the recipe to the newly created Dish. Keep IsFromDatabase as-is (LLM recipes stay LLM).
            recipe.Dish = newDish;
            recipe.OriginalDish = newDish;

            anyChanges = true;
        }

        if (anyChanges)
        {
            await _context.SaveChangesAsync();
        }
    }

    private async Task<int> CheckAndMarkDuplicatesAsync(List<Recipe> recipes, string userId)
    {
        // Load all favorites with ingredients into memory (filtered by userId)
        var allFavorites = await _context.Favorites
            .Include(f => f.Ingredients)
            .Where(f => f.UserId == userId)
            .ToListAsync();

        var duplicatesMarked = 0;

        foreach (var recipe in recipes)
        {
            var duplicate = CheckForDuplicateFavorite(
                allFavorites,
                recipe.Title,
                recipe.Ingredients.Select(i => (i.Name, i.Quantity)).ToList());

            if (duplicate != null)
            {
                // Mark recipe as from database and update it with favorite data
                recipe.IsFromDatabase = true;
                recipe.DetailedRecipe = duplicate.DetailedRecipe;
                recipe.CompleteDescription = duplicate.CompleteDescription;
                recipe.ImageUrl = duplicate.ImageUrl;
                recipe.PreparationTime = duplicate.PreparationTime;
                recipe.CookingTime = duplicate.CookingTime;
                recipe.Kcal = duplicate.Kcal;
                recipe.Servings = duplicate.Servings;
                
                // Update ingredients from favorite (in case favorite has more complete data)
                // Remove existing ingredients and add favorite's ingredients
                _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
                recipe.Ingredients = duplicate.Ingredients.Select(i => new RecipeIngredient
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Category = i.Category
                }).ToList();
                
                duplicatesMarked++;
                
                _logger.LogInformation(
                    "Marked recipe as favorite duplicate. RecipeId: {RecipeId}, FavoriteId: {FavoriteId}, Title: {Title}",
                    recipe.Id,
                    duplicate.Id,
                    recipe.Title);
            }
        }

        return duplicatesMarked;
    }

    private Favorite? CheckForDuplicateFavorite(
        List<Favorite> allFavorites,
        string title,
        List<(string Name, string Quantity)> ingredients)
    {
        // Normalize title for comparison (case-insensitive, trim)
        var normalizedTitle = title.Trim().ToLower();

        // Filter in memory (case-insensitive comparison)
        var candidates = allFavorites
            .Where(f => f.Title.Trim().ToLower() == normalizedTitle)
            .ToList();

        foreach (var candidate in candidates)
        {
            // Check if ingredients match (same count and same name+quantity pairs)
            if (candidate.Ingredients.Count != ingredients.Count)
                continue;

            var candidateIngredients = candidate.Ingredients
                .Select(i => (Name: i.Name.Trim().ToLower(), Quantity: i.Quantity.Trim().ToLower()))
                .OrderBy(i => i.Name)
                .ThenBy(i => i.Quantity)
                .ToList();

            var requestIngredients = ingredients
                .Select(i => (Name: i.Name.Trim().ToLower(), Quantity: i.Quantity.Trim().ToLower()))
                .OrderBy(i => i.Name)
                .ThenBy(i => i.Quantity)
                .ToList();

            if (candidateIngredients.SequenceEqual(requestIngredients, new IngredientEqualityComparer()))
            {
                return candidate;
            }
        }

        return null;
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

