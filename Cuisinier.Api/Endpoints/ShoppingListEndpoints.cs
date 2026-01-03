using Cuisinier.Core.DTOs;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;
using Cuisinier.Infrastructure.Services;
using Cuisinier.Infrastructure.Mappings;
using Microsoft.EntityFrameworkCore;

namespace Cuisinier.Api.Endpoints;

public static class ShoppingListEndpoints
{
    public static void MapShoppingListEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shoppinglist");

        group.MapGet("/{menuId:int}", GetShoppingList)
            .WithName("GetShoppingList");

        group.MapPost("/{menuId:int}/item", AddItem)
            .WithName("AddItem");

        group.MapDelete("/{menuId:int}/item/{itemId:int}", DeleteItem)
            .WithName("DeleteItem");

        group.MapDelete("/{menuId:int}", DeleteShoppingList)
            .WithName("DeleteShoppingList");

        group.MapPost("/{menuId:int}/validate", ValidateShoppingList)
            .WithName("ValidateShoppingList");

        group.MapPost("/{menuId:int}/generate-detailed-recipes", GenerateDetailedRecipes)
            .WithName("GenerateDetailedRecipes");
    }

    private static async Task<IResult> GetShoppingList(
        int menuId,
        CuisinierDbContext context)
    {
        var shoppingList = await context.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.MenuId == menuId);

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(shoppingList.ToResponse());
    }

    private static async Task<IResult> AddItem(
        int menuId,
        AddItemRequest request,
        CuisinierDbContext context)
    {
        var shoppingList = await context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId);

        if (shoppingList == null)
        {
            // Create shopping list if it doesn't exist
            shoppingList = new ShoppingList
            {
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
        CuisinierDbContext context)
    {
        var shoppingList = await context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId);

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
        CuisinierDbContext context)
    {
        var shoppingList = await context.ShoppingLists
            .FirstOrDefaultAsync(l => l.MenuId == menuId);

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        context.ShoppingLists.Remove(shoppingList);
        await context.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> ValidateShoppingList(
        int menuId,
        CuisinierDbContext context)
    {
        var shoppingList = await context.ShoppingLists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.MenuId == menuId);

        if (shoppingList == null)
        {
            return Results.NotFound();
        }

        // List is already saved, just return OK
        return Results.Ok(shoppingList.ToResponse());
    }

    private static async Task<IResult> GenerateDetailedRecipes(
        int menuId,
        CuisinierDbContext context,
        IOpenAIService openAIService,
        ILogger<Program> logger)
    {
        var menu = await context.Menus
            .Include(m => m.Recipes)
                .ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == menuId);

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

