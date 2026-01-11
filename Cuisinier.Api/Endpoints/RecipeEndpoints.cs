using System.Security.Claims;
using Cuisinier.Core.DTOs;
using Cuisinier.Api.Services;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Mappings;
using Cuisinier.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cuisinier.Api.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/recipe");

        group.MapGet("", GetAllRecipes)
            .WithName("GetAllRecipes")
            .RequireAuthorization();

        group.MapGet("/{id:int}", GetRecipe)
            .WithName("GetRecipe")
            .RequireAuthorization();

        group.MapPost("/{id:int}/reuse", ReuseRecipe)
            .WithName("ReuseRecipe")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAllRecipes(
        ClaimsPrincipal user,
        IRecipeQueryService recipeService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var recipes = await recipeService.GetAllRecipesAsync(userId);
        return Results.Ok(recipes);
    }

    private static async Task<IResult> GetRecipe(
        int id,
        ClaimsPrincipal user,
        IRecipeQueryService recipeService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var recipe = await recipeService.GetRecipeAsync(id, userId);
        
        if (recipe == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(recipe);
    }

    private static async Task<IResult> ReuseRecipe(
        int id,
        ReuseRecipeRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IRecipeService recipeService,
        IMemoryCache cache)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Verify menu belongs to user
        var menu = await context.Menus
            .FirstOrDefaultAsync(m => m.Id == request.MenuId && m.UserId == userId);

        if (menu == null)
        {
            return Results.NotFound();
        }

        // Verify recipe belongs to a menu owned by user
        var recipe = await context.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Menu)
            .FirstOrDefaultAsync(r => r.Id == id && r.Menu != null && r.Menu.UserId == userId);

        if (recipe == null)
        {
            return Results.NotFound();
        }

        var newRecipe = await recipeService.ReuseRecipeAsync(recipe, request.MenuId);

        var completeRecipe = await context.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == newRecipe.Id);

        // Invalidate cache when a recipe is reused (new recipe added)
        cache.Remove($"Recipe_All_{userId}");

        return Results.Ok(completeRecipe?.ToResponse());
    }
}

