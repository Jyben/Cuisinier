using Cuisinier.Core.DTOs;
using Cuisinier.Api.Services;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cuisinier.Api.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/recipe");

        group.MapGet("", GetAllRecipes)
            .WithName("GetAllRecipes");

        group.MapGet("/{id:int}", GetRecipe)
            .WithName("GetRecipe");

        group.MapPost("/{id:int}/reuse", ReuseRecipe)
            .WithName("ReuseRecipe");
    }

    private static async Task<IResult> GetAllRecipes(IRecipeQueryService recipeService)
    {
        var recipes = await recipeService.GetAllRecipesAsync();
        return Results.Ok(recipes);
    }

    private static async Task<IResult> GetRecipe(
        int id,
        IRecipeQueryService recipeService)
    {
        var recipe = await recipeService.GetRecipeAsync(id);
        
        if (recipe == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(recipe);
    }

    private static async Task<IResult> ReuseRecipe(
        int id,
        ReuseRecipeRequest request,
        CuisinierDbContext context,
        IRecipeService recipeService,
        IMemoryCache cache)
    {
        var recipe = await context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
        {
            return Results.NotFound();
        }

        var newRecipe = await recipeService.ReuseRecipeAsync(recipe, request.MenuId);

        var completeRecipe = await context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == newRecipe.Id);

        // Invalidate cache when a recipe is reused (new recipe added)
        cache.Remove("Recipe_All");

        return Results.Ok(completeRecipe?.ToResponse());
    }
}

