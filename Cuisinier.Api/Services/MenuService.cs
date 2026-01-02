using System.Text.Json;
using Cuisinier.Core.DTOs;
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
        ILogger<MenuService> logger)
    {
        _context = context;
        _openAIService = openAIService;
        _backgroundRecipeService = backgroundRecipeService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MenuResponse> GenerateMenuAsync(MenuGenerationRequest request)
    {
        _logger.LogInformation(
            "Generating menu. WeekStartDate: {WeekStartDate}, RecipeIdsCount: {RecipeIdsCount}",
            request.Parameters.WeekStartDate,
            request.RecipeIds?.Count ?? 0);

        // Use transaction to ensure atomicity of menu generation
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Delete all non-validated menus (menus without shopping list) before generating a new one
            var nonValidatedMenus = await _context.Menus
                .Where(m => !_context.ShoppingLists.Any(sl => sl.MenuId == m.Id))
                .ToListAsync();

            if (nonValidatedMenus.Any())
            {
                _logger.LogInformation(
                    "Deleting {Count} non-validated menus before generating new menu",
                    nonValidatedMenus.Count);
                _context.Menus.RemoveRange(nonValidatedMenus);
                await _context.SaveChangesAsync();
            }

            var menuResponse = await _openAIService.GenerateMenuAsync(request.Parameters);

            // Save parameters in MenuSettings entity (independent of the menu)
            await SaveParametersInternalAsync(request.Parameters);

            // Create menu in database (without parameters, they are now in MenuSettings)
            var menu = new Menu
            {
                WeekStartDate = request.Parameters.WeekStartDate,
                CreationDate = DateTime.UtcNow,
                GenerationParametersJson = null
            };

            // If existing recipes are provided, reuse them
            if (request.RecipeIds != null && request.RecipeIds.Any())
            {
                var existingRecipes = await _context.Recipes
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
            foreach (var recipeResponse in menuResponse.Recipes)
            {
                var recipe = recipeResponse.ToEntity(menu.Id);
                menu.Recipes.Add(recipe);
            }

            _context.Menus.Add(menu);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Menu generated successfully. MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                menu.Id,
                menu.Recipes.Count);

            // Load with relationships for response
            var completeMenu = await _context.Menus
                .Include(m => m.Recipes)
                    .ThenInclude(r => r.Ingredients)
                .FirstOrDefaultAsync(m => m.Id == menu.Id);

            return completeMenu?.ToResponse() 
                ?? throw new InvalidOperationException("Failed to load generated menu");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<MenuResponse?> GetMenuAsync(int id)
    {
        var cacheKey = $"{MenuCacheKeyPrefix}{id}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out MenuResponse? cachedMenu))
        {
            _logger.LogInformation("Retrieved menu from cache. MenuId: {MenuId}", id);
            return cachedMenu;
        }

        _logger.LogInformation("Retrieving menu from database. MenuId: {MenuId}", id);

        var menu = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (menu == null)
        {
            _logger.LogWarning("Menu not found. MenuId: {MenuId}", id);
            return null;
        }

        var menuResponse = menu.ToResponse();
        
        // Cache the result (only cache validated menus to avoid caching temporary data)
        var isValidated = await _context.ShoppingLists.AnyAsync(sl => sl.MenuId == id);
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

    public async Task<List<MenuResponse>> GetAllMenusAsync()
    {
        // Try to get from cache first
        if (_cache.TryGetValue(AllMenusCacheKey, out List<MenuResponse>? cachedMenus))
        {
            _logger.LogInformation("Retrieved all menus from cache. Count: {Count}", cachedMenus?.Count ?? 0);
            return cachedMenus ?? new List<MenuResponse>();
        }

        _logger.LogInformation("Retrieving all menus from database");

        // Only return validated menus (menus that have a shopping list)
        var menus = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .Where(m => _context.ShoppingLists.Any(sl => sl.MenuId == m.Id))
            .OrderByDescending(m => m.WeekStartDate)
            .ToListAsync();

        var menuResponses = menus.Select(m => m.ToResponse()).ToList();
        
        // Cache the result
        _cache.Set(AllMenusCacheKey, menuResponses, MenuCacheExpiration);
        
        _logger.LogInformation("All menus retrieved and cached. Count: {Count}", menuResponses.Count);

        return menuResponses;
    }

    public async Task<MenuParameters?> GetLastParametersAsync()
    {
        // Try to get from cache first
        if (_cache.TryGetValue(LastParametersCacheKey, out MenuParameters? cachedParameters))
        {
            _logger.LogInformation("Retrieved last menu parameters from cache");
            return cachedParameters;
        }

        // Get parameters from MenuSettings entity (ID = 1)
        var menuSettings = await _context.MenuSettings.FindAsync(1);

        if (menuSettings == null || string.IsNullOrEmpty(menuSettings.ParametersJson))
        {
            return null;
        }

        try
        {
            var jsonOptions = JsonOptionsHelper.GetDefaultOptions();
            var parameters = JsonSerializer.Deserialize<MenuParameters>(menuSettings.ParametersJson, jsonOptions);
            
            // Cache the result
            _cache.Set(LastParametersCacheKey, parameters, CacheExpiration);
            
            _logger.LogInformation("Retrieved last menu parameters successfully from database and cached");
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

    public async Task SaveParametersAsync(MenuParameters parameters)
    {
        await SaveParametersInternalAsync(parameters);
    }

    private async Task SaveParametersInternalAsync(MenuParameters parameters)
    {
        var jsonOptions = JsonOptionsHelper.GetSerializationOptions();
        
        // Create a complete deep copy via serialization/deserialization to avoid any reference issues
        var parametersJson = JsonSerializer.Serialize(parameters, jsonOptions);
        var parametersForSave = JsonSerializer.Deserialize<MenuParameters>(parametersJson, jsonOptions) 
            ?? new MenuParameters();
        
        // Reset the date so it's not saved (it will be recalculated each time)
        parametersForSave.WeekStartDate = default;

        // Get or create MenuSettings record (ID = 1)
        var menuSettings = await _context.MenuSettings.FindAsync(1);
        
        if (menuSettings == null)
        {
            menuSettings = new MenuSettings
            {
                Id = 1,
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
        
        // Invalidate cache when parameters are saved
        _cache.Remove(LastParametersCacheKey);
        _logger.LogInformation("Menu parameters saved and cache invalidated");
    }

    public async Task<RecipeResponse> ReplaceRecipeAsync(int menuId, int recipeId, RecipeReplacementRequest request)
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
        var newRecipeResponse = await _openAIService.ReplaceRecipeAsync(request.Parameters, recipeResponse);

        Recipe newRecipe;
        
        // Use transaction to ensure atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Remove old recipe
            _context.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();

            // Add new one
            newRecipe = newRecipeResponse.ToEntity(menuId);
            _context.Recipes.Add(newRecipe);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        var completeRecipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == newRecipe.Id);

        _logger.LogInformation(
            "Recipe replaced successfully. MenuId: {MenuId}, OldRecipeId: {OldRecipeId}, NewRecipeId: {NewRecipeId}",
            menuId,
            recipeId,
            newRecipe.Id);

        return completeRecipe?.ToResponse() 
            ?? throw new InvalidOperationException("Failed to load replaced recipe");
    }

    public async Task<RecipeResponse> ReplaceIngredientAsync(int menuId, int recipeId, IngredientReplacementRequest request)
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
        _cache.Remove($"{MenuCacheKeyPrefix}{menuId}");
        _cache.Remove($"Recipe_{recipeId}");

        return completeRecipe?.ToResponse() 
            ?? throw new InvalidOperationException("Failed to load recipe after ingredient replacement");
    }

    public async Task DeleteRecipeAsync(int menuId, int recipeId)
    {
        _logger.LogInformation(
            "Deleting recipe. MenuId: {MenuId}, RecipeId: {RecipeId}",
            menuId,
            recipeId);

        var recipe = await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == recipeId && r.MenuId == menuId);

        if (recipe == null)
        {
            throw new KeyNotFoundException($"Recipe {recipeId} not found in menu {menuId}");
        }

        _context.Recipes.Remove(recipe);
        await _context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"{MenuCacheKeyPrefix}{menuId}");
        _cache.Remove(AllMenusCacheKey);
        _cache.Remove($"Recipe_{recipeId}");

        _logger.LogInformation(
            "Recipe deleted successfully and cache invalidated. MenuId: {MenuId}, RecipeId: {RecipeId}",
            menuId,
            recipeId);
    }

    public async Task DeleteMenuAsync(int menuId)
    {
        _logger.LogInformation("Deleting menu. MenuId: {MenuId}", menuId);

        var menu = await _context.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId);

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found");
        }

        _context.Menus.Remove(menu);
        await _context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"{MenuCacheKeyPrefix}{menuId}");
        _cache.Remove(AllMenusCacheKey);

        _logger.LogInformation("Menu deleted successfully and cache invalidated. MenuId: {MenuId}", menuId);
    }

    public async Task<MenuResponse> ValidateMenuAsync(int menuId)
    {
        _logger.LogInformation("Validating menu. MenuId: {MenuId}", menuId);

        var menu = await _context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        if (menu == null)
        {
            throw new KeyNotFoundException($"Menu {menuId} not found");
        }

        // Check if shopping list already exists
        var existingShoppingList = await _context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId);

        if (existingShoppingList == null)
        {
            // Generate shopping list synchronously (user needs it immediately)
            var recipesResponse = menu.Recipes.Select(r => r.ToResponse()).ToList();
            var shoppingListResponse = await _openAIService.GenerateShoppingListAsync(recipesResponse);

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

            _context.ShoppingLists.Add(shoppingList);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Shopping list generated for menu. MenuId: {MenuId}, ItemsCount: {ItemsCount}",
                menuId,
                shoppingList.Items.Count);
            
            // Invalidate cache when menu is validated (it becomes a validated menu)
            _cache.Remove($"{MenuCacheKeyPrefix}{menuId}");
            _cache.Remove(AllMenusCacheKey);
        }

        // Launch detailed recipe generation in background
        var recipesWithoutDetail = menu.Recipes
            .Where(r => string.IsNullOrEmpty(r.DetailedRecipe))
            .Select(r => r.Id)
            .ToList();

        if (recipesWithoutDetail.Any())
        {
            _logger.LogInformation(
                "Launching background detailed recipe generation. MenuId: {MenuId}, RecipesCount: {RecipesCount}",
                menuId,
                recipesWithoutDetail.Count);
            await _backgroundRecipeService.GenerateDetailedRecipesAsync(menuId, recipesWithoutDetail);
        }

        _logger.LogInformation("Menu validated successfully. MenuId: {MenuId}", menuId);

        return menu.ToResponse();
    }
}

