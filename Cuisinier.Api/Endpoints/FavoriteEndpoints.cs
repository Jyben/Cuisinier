using System.Security.Claims;
using Cuisinier.Shared.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Cuisinier.Api.Endpoints;

public static class FavoriteEndpoints
{
    public static void MapFavoriteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/favorite");

        group.MapGet("", GetAllFavorites)
            .WithName("GetAllFavorites")
            .WithSummary("Get all favorites")
            .Produces<List<FavoriteResponse>>(StatusCodes.Status200OK)
            .RequireAuthorization();

        group.MapGet("/{id:int}", GetFavorite)
            .WithName("GetFavorite")
            .WithSummary("Get favorite by ID")
            .Produces<FavoriteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("", AddFavorite)
            .WithName("AddFavorite")
            .WithSummary("Add a recipe to favorites")
            .Produces<FavoriteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPut("/{id:int}", UpdateFavorite)
            .WithName("UpdateFavorite")
            .WithSummary("Update a favorite recipe")
            .Produces<FavoriteResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapDelete("/{id:int}", DeleteFavorite)
            .WithName("DeleteFavorite")
            .WithSummary("Delete a favorite recipe")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("/check-duplicate", CheckDuplicate)
            .WithName("CheckDuplicate")
            .WithSummary("Check if a recipe already exists in favorites")
            .Produces<bool>(StatusCodes.Status200OK)
            .RequireAuthorization();

        group.MapPost("/check-duplicates-batch", CheckDuplicatesBatch)
            .WithName("CheckDuplicatesBatch")
            .WithSummary("Check multiple recipes for duplicates in favorites")
            .Produces<CheckDuplicatesBatchResponse>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAllFavorites(
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var favorites = await context.Favorites
            .Include(f => f.Ingredients)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        var response = favorites.Select(f => ToResponse(f)).ToList();
        return Results.Ok(response);
    }

    private static async Task<IResult> GetFavorite(
        int id,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var favorite = await context.Favorites
            .Include(f => f.Ingredients)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (favorite == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToResponse(favorite));
    }

    private static async Task<IResult> AddFavorite(
        FavoriteRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check for duplicate (same title and same ingredients)
        var existingFavorite = await CheckForDuplicateAsync(
            context,
            userId,
            request.Title,
            request.Ingredients.Select(i => (i.Name, i.Quantity)).ToList());

        if (existingFavorite != null)
        {
            return Results.Conflict(new { message = "Un plat avec le même nom et les mêmes ingrédients existe déjà dans les favoris." });
        }

        var favorite = new Favorite
        {
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            CompleteDescription = request.CompleteDescription,
            DetailedRecipe = request.DetailedRecipe,
            ImageUrl = request.ImageUrl,
            PreparationTime = request.PreparationTime,
            CookingTime = request.CookingTime,
            Kcal = request.Kcal,
            Servings = request.Servings,
            CreatedAt = DateTime.UtcNow,
            Ingredients = request.Ingredients.Select(i => new FavoriteIngredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };

        context.Favorites.Add(favorite);
        await context.SaveChangesAsync();

        await context.Entry(favorite)
            .Collection(f => f.Ingredients)
            .LoadAsync();

        return Results.Created($"/api/favorite/{favorite.Id}", ToResponse(favorite));
    }

    private static async Task<IResult> UpdateFavorite(
        int id,
        FavoriteRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var favorite = await context.Favorites
            .Include(f => f.Ingredients)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (favorite == null)
        {
            return Results.NotFound();
        }

        // Check for duplicate (excluding current favorite)
        var existingFavorite = await CheckForDuplicateAsync(
            context,
            userId,
            request.Title,
            request.Ingredients.Select(i => (i.Name, i.Quantity)).ToList(),
            excludeId: id);

        if (existingFavorite != null)
        {
            return Results.Conflict(new { message = "Un plat avec le même nom et les mêmes ingrédients existe déjà dans les favoris." });
        }

        // Update favorite
        favorite.Title = request.Title;
        favorite.Description = request.Description;
        favorite.CompleteDescription = request.CompleteDescription;
        favorite.DetailedRecipe = request.DetailedRecipe;
        favorite.ImageUrl = request.ImageUrl;
        favorite.PreparationTime = request.PreparationTime;
        favorite.CookingTime = request.CookingTime;
        favorite.Kcal = request.Kcal;
        favorite.Servings = request.Servings;
        favorite.UpdatedAt = DateTime.UtcNow;

        // Update ingredients
        context.FavoriteIngredients.RemoveRange(favorite.Ingredients);
        favorite.Ingredients = request.Ingredients.Select(i => new FavoriteIngredient
        {
            Name = i.Name,
            Quantity = i.Quantity,
            Category = i.Category
        }).ToList();

        await context.SaveChangesAsync();

        await context.Entry(favorite)
            .Collection(f => f.Ingredients)
            .LoadAsync();

        return Results.Ok(ToResponse(favorite));
    }

    private static async Task<IResult> DeleteFavorite(
        int id,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var favorite = await context.Favorites
            .Include(f => f.Ingredients)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (favorite == null)
        {
            return Results.NotFound();
        }

        context.Favorites.Remove(favorite);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> CheckDuplicate(
        CheckDuplicateRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var exists = await CheckForDuplicateAsync(
            context,
            userId,
            request.Title,
            request.Ingredients.Select(i => (i.Name, i.Quantity)).ToList()) != null;

        return Results.Ok(exists);
    }

    private static async Task<IResult> CheckDuplicatesBatch(
        CheckDuplicatesBatchRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Load all favorites once for better performance (filtered by userId)
        var allFavorites = await context.Favorites
            .Include(f => f.Ingredients)
            .Where(f => f.UserId == userId)
            .ToListAsync();

        var matches = new List<RecipeDuplicateMatch>();

        foreach (var recipe in request.Recipes)
        {
            var duplicate = CheckForDuplicateInMemory(
                allFavorites,
                recipe.Title,
                recipe.Ingredients.Select(i => (i.Name, i.Quantity)).ToList());

            if (duplicate != null)
            {
                matches.Add(new RecipeDuplicateMatch
                {
                    RecipeId = recipe.RecipeId,
                    FavoriteId = duplicate.Id
                });
            }
        }

        var response = new CheckDuplicatesBatchResponse
        {
            Matches = matches
        };

        return Results.Ok(response);
    }

    private static Favorite? CheckForDuplicateInMemory(
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

            if (candidateIngredients.SequenceEqual(requestIngredients, new IngredientComparer()))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<Favorite?> CheckForDuplicateAsync(
        CuisinierDbContext context,
        string userId,
        string title,
        List<(string Name, string Quantity)> ingredients,
        int? excludeId = null)
    {
        // Normalize title for comparison (case-insensitive, trim)
        var normalizedTitle = title.Trim().ToLower();

        // Load all favorites with ingredients into memory first (filtered by userId)
        var allFavorites = await context.Favorites
            .Include(f => f.Ingredients)
            .Where(f => f.UserId == userId)
            .ToListAsync();

        // Filter in memory (case-insensitive comparison)
        var candidates = allFavorites
            .Where(f => f.Title.Trim().ToLower() == normalizedTitle)
            .ToList();

        if (excludeId.HasValue)
        {
            candidates = candidates.Where(f => f.Id != excludeId.Value).ToList();
        }

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

            if (candidateIngredients.SequenceEqual(requestIngredients, new IngredientComparer()))
            {
                return candidate;
            }
        }

        return null;
    }

    private static FavoriteResponse ToResponse(Favorite favorite)
    {
        return new FavoriteResponse
        {
            Id = favorite.Id,
            Title = favorite.Title,
            Description = favorite.Description,
            CompleteDescription = favorite.CompleteDescription,
            DetailedRecipe = favorite.DetailedRecipe,
            ImageUrl = favorite.ImageUrl,
            PreparationTime = favorite.PreparationTime,
            CookingTime = favorite.CookingTime,
            Kcal = favorite.Kcal,
            Servings = favorite.Servings,
            CreatedAt = favorite.CreatedAt,
            UpdatedAt = favorite.UpdatedAt,
            Ingredients = favorite.Ingredients.Select(i => new IngredientResponse
            {
                Id = i.Id,
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };
    }

    private class IngredientComparer : IEqualityComparer<(string Name, string Quantity)>
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

public class CheckDuplicateRequest
{
    public string Title { get; set; } = string.Empty;
    public List<IngredientRequest> Ingredients { get; set; } = new();
}
