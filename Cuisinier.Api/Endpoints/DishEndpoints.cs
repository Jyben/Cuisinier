using System.Security.Claims;
using Cuisinier.Shared.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Mappings;
using Cuisinier.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Cuisinier.Api.Endpoints;

public static class DishEndpoints
{
    public static void MapDishEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dish");

        group.MapGet("", GetAllDishes)
            .WithName("GetAllDishes")
            .WithSummary("Get all dishes with optional filtering")
            .Produces<DishListResponse>(StatusCodes.Status200OK)
            .RequireAuthorization();

        group.MapGet("/{id:int}", GetDish)
            .WithName("GetDish")
            .WithSummary("Get dish by ID")
            .Produces<DishResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("", AddDish)
            .WithName("AddDish")
            .WithSummary("Add a new dish")
            .Produces<DishResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapPut("/{id:int}", UpdateDish)
            .WithName("UpdateDish")
            .WithSummary("Update a dish")
            .Produces<DishResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization();

        group.MapDelete("/{id:int}", DeleteDish)
            .WithName("DeleteDish")
            .WithSummary("Delete a dish")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        group.MapPost("/check-duplicate", CheckDuplicate)
            .WithName("CheckDishDuplicate")
            .WithSummary("Check if a dish already exists")
            .Produces<bool>(StatusCodes.Status200OK)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAllDishes(
        [AsParameters] DishFilterRequest filter,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Dishes are shared (not user-specific), so we don't filter by UserId
        var query = context.Dishes
            .Include(d => d.Ingredients)
            .AsQueryable();

        // Apply favorites filter - filter dishes that match user's favorite titles
        if (filter.FavoritesOnly)
        {
            // Use a subquery join approach with case-insensitive comparison
            query = query.Where(d => context.Favorites
                .Any(f => f.UserId == userId && f.Title.ToLower() == d.Title.ToLower()));
        }

        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.Trim().ToLower();
            query = query.Where(d => 
                d.Title.ToLower().Contains(searchTerm) ||
                d.Description.ToLower().Contains(searchTerm) ||
                d.Ingredients.Any(i => i.Name.ToLower().Contains(searchTerm)));
        }

        // Apply category filter
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(d => 
                d.Ingredients.Any(i => i.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply Kcal filters
        if (filter.MinKcal.HasValue)
        {
            query = query.Where(d => d.Kcal >= filter.MinKcal.Value);
        }
        if (filter.MaxKcal.HasValue)
        {
            query = query.Where(d => d.Kcal <= filter.MaxKcal.Value || d.Kcal == null);
        }

        // Apply preparation time filter
        if (filter.MaxPreparationTime.HasValue)
        {
            query = query.Where(d => 
                d.PreparationTime <= filter.MaxPreparationTime.Value || d.PreparationTime == null);
        }

        // Apply cooking time filter
        if (filter.MaxCookingTime.HasValue)
        {
            query = query.Where(d => 
                d.CookingTime <= filter.MaxCookingTime.Value || d.CookingTime == null);
        }

        // Apply servings filters
        if (filter.MinServings.HasValue)
        {
            query = query.Where(d => d.Servings >= filter.MinServings.Value);
        }
        if (filter.MaxServings.HasValue)
        {
            query = query.Where(d => d.Servings <= filter.MaxServings.Value);
        }

        // Apply ingredient name filter
        if (!string.IsNullOrWhiteSpace(filter.IngredientName))
        {
            var ingredientName = filter.IngredientName.Trim().ToLower();
            query = query.Where(d => 
                d.Ingredients.Any(i => i.Name.ToLower().Contains(ingredientName)));
        }

        // Apply sorting
        query = filter.SortBy?.ToLower() switch
        {
            "title" => filter.SortDescending 
                ? query.OrderByDescending(d => d.Title)
                : query.OrderBy(d => d.Title),
            "createdat" => filter.SortDescending
                ? query.OrderByDescending(d => d.CreatedAt)
                : query.OrderBy(d => d.CreatedAt),
            "kcal" => filter.SortDescending
                ? query.OrderByDescending(d => d.Kcal ?? int.MaxValue)
                : query.OrderBy(d => d.Kcal ?? int.MaxValue),
            _ => query.OrderByDescending(d => d.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        // Apply pagination
        var dishes = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var response = new DishListResponse
        {
            Dishes = dishes.Select(d => d.ToResponse()).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return Results.Ok(response);
    }

    private static async Task<IResult> GetDish(
        int id,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Dishes are shared (not user-specific), so we don't filter by UserId
        var dish = await context.Dishes
            .Include(d => d.Ingredients)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dish == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(dish.ToResponse());
    }

    private static async Task<IResult> AddDish(
        DishRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check for duplicate (same title and same ingredients)
        // Dishes are shared (not user-specific), so we check globally
        var existingDish = await CheckForDuplicateAsync(
            context,
            null, // No userId filter - dishes are global
            request.Title,
            request.Ingredients.Select(i => (i.Name, i.Quantity)).ToList());

        if (existingDish != null)
        {
            return Results.Conflict(new { message = "Un plat avec le même nom et les mêmes ingrédients existe déjà." });
        }

        var dish = new Dish
        {
            UserId = null, // Dishes are shared (not user-specific)
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
            Ingredients = request.Ingredients.Select(i => new DishIngredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };

        context.Dishes.Add(dish);
        await context.SaveChangesAsync();

        await context.Entry(dish)
            .Collection(d => d.Ingredients)
            .LoadAsync();

        return Results.Created($"/api/dish/{dish.Id}", dish.ToResponse());
    }

    private static async Task<IResult> UpdateDish(
        int id,
        DishRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Dishes are shared (not user-specific), so we don't filter by UserId
        var dish = await context.Dishes
            .Include(d => d.Ingredients)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dish == null)
        {
            return Results.NotFound();
        }

        // Check for duplicate (excluding current dish)
        // Dishes are shared (not user-specific), so we check globally
        var existingDish = await CheckForDuplicateAsync(
            context,
            null, // No userId filter - dishes are global
            request.Title,
            request.Ingredients.Select(i => (i.Name, i.Quantity)).ToList(),
            excludeId: id);

        if (existingDish != null)
        {
            return Results.Conflict(new { message = "Un plat avec le même nom et les mêmes ingrédients existe déjà." });
        }

        // Update dish
        dish.Title = request.Title;
        dish.Description = request.Description;
        dish.CompleteDescription = request.CompleteDescription;
        dish.DetailedRecipe = request.DetailedRecipe;
        dish.ImageUrl = request.ImageUrl;
        dish.PreparationTime = request.PreparationTime;
        dish.CookingTime = request.CookingTime;
        dish.Kcal = request.Kcal;
        dish.Servings = request.Servings;
        dish.UpdatedAt = DateTime.UtcNow;

        // Update ingredients
        context.DishIngredients.RemoveRange(dish.Ingredients);
        dish.Ingredients = request.Ingredients.Select(i => new DishIngredient
        {
            Name = i.Name,
            Quantity = i.Quantity,
            Category = i.Category
        }).ToList();

        await context.SaveChangesAsync();

        await context.Entry(dish)
            .Collection(d => d.Ingredients)
            .LoadAsync();

        return Results.Ok(dish.ToResponse());
    }

    private static async Task<IResult> DeleteDish(
        int id,
        ClaimsPrincipal user,
        CuisinierDbContext context)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Dishes are shared (not user-specific), so we don't filter by UserId
        var dish = await context.Dishes
            .Include(d => d.Ingredients)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dish == null)
        {
            return Results.NotFound();
        }

        // Check if any Recipes reference this Dish via OriginalDishId
        // (DishId is handled automatically with SetNull behavior)
        var recipesWithOriginalDish = await context.Recipes
            .Where(r => r.OriginalDishId == id)
            .ToListAsync();

        // Update Recipes to remove OriginalDishId reference before deleting the Dish
        // (OriginalDishId has NoAction delete behavior, so we must handle it manually)
        foreach (var recipe in recipesWithOriginalDish)
        {
            recipe.OriginalDishId = null;
        }

        // Also check for ShoppingListDish references (they should be cascade deleted, but let's be safe)
        var shoppingListDishes = await context.Set<ShoppingListDish>()
            .Where(sld => sld.DishId == id)
            .ToListAsync();

        if (shoppingListDishes.Any())
        {
            context.Set<ShoppingListDish>().RemoveRange(shoppingListDishes);
        }

        context.Dishes.Remove(dish);
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

        // Dishes are shared (not user-specific), so we check globally
        var exists = await CheckForDuplicateAsync(
            context,
            null, // No userId filter - dishes are global
            request.Title,
            request.Ingredients.Select(i => (i.Name, i.Quantity)).ToList()) != null;

        return Results.Ok(exists);
    }

    private static async Task<Dish?> CheckForDuplicateAsync(
        CuisinierDbContext context,
        string? userId, // Nullable - dishes are shared, so userId can be null
        string title,
        List<(string Name, string Quantity)> ingredients,
        int? excludeId = null)
    {
        // Normalize title for comparison (case-insensitive, trim)
        var normalizedTitle = title.Trim().ToLower();

        // Load all dishes with ingredients into memory first
        // Dishes are shared (not user-specific), so we don't filter by userId
        var allDishes = await context.Dishes
            .Include(d => d.Ingredients)
            .ToListAsync();

        // Filter in memory (case-insensitive comparison)
        var candidates = allDishes
            .Where(d => d.Title.Trim().ToLower() == normalizedTitle)
            .ToList();

        if (excludeId.HasValue)
        {
            candidates = candidates.Where(d => d.Id != excludeId.Value).ToList();
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
