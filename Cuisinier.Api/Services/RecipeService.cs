using Cuisinier.Core.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cuisinier.Api.Services;

public class RecipeQueryService : IRecipeQueryService
{
    private readonly CuisinierDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RecipeQueryService> _logger;

    private const string AllRecipesCacheKey = "Recipe_All";
    private const string RecipeCacheKeyPrefix = "Recipe_";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public RecipeQueryService(
        CuisinierDbContext context,
        IMemoryCache cache,
        ILogger<RecipeQueryService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<RecipeResponse>> GetAllRecipesAsync(string userId)
    {
        var cacheKey = $"{AllRecipesCacheKey}_{userId}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<RecipeResponse>? cachedRecipes))
        {
            _logger.LogInformation("Retrieved all recipes from cache. Count: {Count}, UserId: {UserId}", cachedRecipes?.Count ?? 0, userId);
            return cachedRecipes ?? new List<RecipeResponse>();
        }

        _logger.LogInformation("Retrieving all recipes from database. UserId: {UserId}", userId);

        // Get all recipes marked as being from the database for this user (via Menu)
        var recipes = await _context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Menu)
            .Where(r => r.Menu != null && r.Menu.UserId == userId && (r.IsFromDatabase || r.OriginalDishId == null))
            .ToListAsync();

        var recipeResponses = recipes.Select(r => r.ToResponse()).ToList();
        
        // Cache the result
        _cache.Set(cacheKey, recipeResponses, CacheExpiration);
        
        _logger.LogInformation("All recipes retrieved and cached. Count: {Count}, UserId: {UserId}", recipeResponses.Count, userId);

        return recipeResponses;
    }

    public async Task<RecipeResponse?> GetRecipeAsync(int id, string userId)
    {
        var cacheKey = $"{RecipeCacheKeyPrefix}{userId}_{id}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out RecipeResponse? cachedRecipe))
        {
            _logger.LogInformation("Retrieved recipe from cache. RecipeId: {RecipeId}, UserId: {UserId}", id, userId);
            return cachedRecipe;
        }

        _logger.LogInformation("Retrieving recipe from database. RecipeId: {RecipeId}, UserId: {UserId}", id, userId);

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Menu)
            .FirstOrDefaultAsync(r => r.Id == id && r.Menu != null && r.Menu.UserId == userId);

        if (recipe == null)
        {
            _logger.LogWarning("Recipe not found. RecipeId: {RecipeId}, UserId: {UserId}", id, userId);
            return null;
        }

        var recipeResponse = recipe.ToResponse();
        
        // Cache the result
        _cache.Set(cacheKey, recipeResponse, CacheExpiration);
        
        _logger.LogInformation("Recipe retrieved and cached. RecipeId: {RecipeId}, UserId: {UserId}", id, userId);

        return recipeResponse;
    }
}

