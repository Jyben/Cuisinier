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

    public async Task<List<RecipeResponse>> GetAllRecipesAsync()
    {
        // Try to get from cache first
        if (_cache.TryGetValue(AllRecipesCacheKey, out List<RecipeResponse>? cachedRecipes))
        {
            _logger.LogInformation("Retrieved all recipes from cache. Count: {Count}", cachedRecipes?.Count ?? 0);
            return cachedRecipes ?? new List<RecipeResponse>();
        }

        _logger.LogInformation("Retrieving all recipes from database");

        // Get all recipes marked as being from the database
        var recipes = await _context.Recipes
            .Include(r => r.Ingredients)
            .Where(r => r.IsFromDatabase || r.OriginalDishId == null)
            .ToListAsync();

        var recipeResponses = recipes.Select(r => r.ToResponse()).ToList();
        
        // Cache the result
        _cache.Set(AllRecipesCacheKey, recipeResponses, CacheExpiration);
        
        _logger.LogInformation("All recipes retrieved and cached. Count: {Count}", recipeResponses.Count);

        return recipeResponses;
    }

    public async Task<RecipeResponse?> GetRecipeAsync(int id)
    {
        var cacheKey = $"{RecipeCacheKeyPrefix}{id}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out RecipeResponse? cachedRecipe))
        {
            _logger.LogInformation("Retrieved recipe from cache. RecipeId: {RecipeId}", id);
            return cachedRecipe;
        }

        _logger.LogInformation("Retrieving recipe from database. RecipeId: {RecipeId}", id);

        var recipe = await _context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
        {
            _logger.LogWarning("Recipe not found. RecipeId: {RecipeId}", id);
            return null;
        }

        var recipeResponse = recipe.ToResponse();
        
        // Cache the result
        _cache.Set(cacheKey, recipeResponse, CacheExpiration);
        
        _logger.LogInformation("Recipe retrieved and cached. RecipeId: {RecipeId}", id);

        return recipeResponse;
    }
}

