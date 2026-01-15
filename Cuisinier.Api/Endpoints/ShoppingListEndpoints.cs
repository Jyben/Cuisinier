using System.Security.Claims;
using Cuisinier.Shared.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Infrastructure.Mappings;
using Cuisinier.Api.Helpers;
using Cuisinier.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cuisinier.Api.Endpoints;

public static class ShoppingListEndpoints
{
    public static void MapShoppingListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shoppinglist");

        group.MapGet("/{menuId:int}", GetShoppingList)
            .WithName("GetShoppingList")
            .RequireAuthorization();

        group.MapPost("/{menuId:int}/item", AddItem)
            .WithName("AddItem")
            .RequireAuthorization();

        group.MapDelete("/{menuId:int}/item/{itemId:int}", DeleteItem)
            .WithName("DeleteItem")
            .RequireAuthorization();

        group.MapDelete("/{menuId:int}", DeleteShoppingList)
            .WithName("DeleteShoppingList")
            .RequireAuthorization();

        group.MapPost("/{menuId:int}/validate", ValidateShoppingList)
            .WithName("ValidateShoppingList")
            .RequireAuthorization();

        group.MapPost("/{menuId:int}/generate-detailed-recipes", GenerateDetailedRecipes)
            .WithName("GenerateDetailedRecipes")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetShoppingList(
        int menuId,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IUserAccessService userAccessService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accessibleUserIds = await userAccessService.GetAccessibleUserIdsAsync(userId);

        var shoppingList = await context.ShoppingLists
            .Include(l => l.Items)
            .Include(l => l.Menu)
            .Where(l => l.MenuId == menuId && accessibleUserIds.Contains(l.UserId))
            .FirstOrDefaultAsync();

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(shoppingList.ToResponse());
    }

    private static async Task<IResult> AddItem(
        int menuId,
        AddItemRequest request,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IUserAccessService userAccessService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accessibleUserIds = await userAccessService.GetAccessibleUserIdsAsync(userId);

        // Check that menu belongs to accessible users
        var menu = await context.Menus
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            return Results.NotFound();
        }

        var shoppingList = await context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId && accessibleUserIds.Contains(l.UserId));

        if (shoppingList == null)
        {
            // Create shopping list if it doesn't exist (owned by menu owner)
            shoppingList = new ShoppingList
            {
                UserId = menu.UserId,
                MenuId = menuId,
                CreationDate = DateTime.UtcNow
            };
            context.ShoppingLists.Add(shoppingList);
            await context.SaveChangesAsync();
        }

        var item = new ShoppingListItem
        {
            ShoppingListId = shoppingList.Id,
            Name = request.Name,
            Quantity = request.Quantity,
            Category = request.Category,
            IsManuallyAdded = true
        };

        context.ShoppingListItems.Add(item);
        await context.SaveChangesAsync();

        var completeItem = await context.ShoppingListItems
            .FirstOrDefaultAsync(i => i.Id == item.Id);

        return Results.Ok(completeItem?.ToResponse());
    }

    private static async Task<IResult> DeleteItem(
        int menuId,
        int itemId,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IUserAccessService userAccessService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accessibleUserIds = await userAccessService.GetAccessibleUserIdsAsync(userId);

        var shoppingList = await context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId && accessibleUserIds.Contains(l.UserId));

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        var item = await context.ShoppingListItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ShoppingListId == shoppingList.Id);

        if (item == null)
        {
            return Results.NotFound();
        }

        context.ShoppingListItems.Remove(item);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteShoppingList(
        int menuId,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IUserAccessService userAccessService,
        IMemoryCache cache)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accessibleUserIds = await userAccessService.GetAccessibleUserIdsAsync(userId);

        var shoppingList = await context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId && accessibleUserIds.Contains(l.UserId));

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        context.ShoppingLists.Remove(shoppingList);
        await context.SaveChangesAsync();

        // Invalidate menus cache for all accessible users
        foreach (var uid in accessibleUserIds)
        {
            cache.Remove($"Menu_All_{uid}");
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ValidateShoppingList(
        int menuId,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IUserAccessService userAccessService)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accessibleUserIds = await userAccessService.GetAccessibleUserIdsAsync(userId);

        var shoppingList = await context.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.MenuId == menuId && accessibleUserIds.Contains(l.UserId));

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        // List is already saved, just return OK
        return Results.Ok(shoppingList.ToResponse());
    }

    private static async Task<IResult> GenerateDetailedRecipes(
        int menuId,
        ClaimsPrincipal user,
        CuisinierDbContext context,
        IOpenAIService openAIService,
        IUserAccessService userAccessService,
        ILogger<Program> logger)
    {
        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var accessibleUserIds = await userAccessService.GetAccessibleUserIdsAsync(userId);

        var menu = await context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == menuId && accessibleUserIds.Contains(m.UserId));

        if (menu == null)
        {
            return Results.NotFound();
        }

        // Generate detailed recipes for all recipes that don't have one yet
        foreach (var recipe in menu.Recipes.Where(r => string.IsNullOrEmpty(r.CompleteDescription)))
        {
            var recipeResponse = recipe.ToResponse();
            var completeDescription = await openAIService.GenerateDetailedRecipeAsync(
                recipe.Title,
                recipeResponse.Ingredients,
                recipe.Description);

            recipe.CompleteDescription = completeDescription;
        }

        await context.SaveChangesAsync();

        var completeMenu = await context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == menuId);

        return Results.Ok(completeMenu?.ToResponse());
    }
}

