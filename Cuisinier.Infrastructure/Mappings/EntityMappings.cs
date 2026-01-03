using System.Text.Json;
using Cuisinier.Core.Entities;
using Cuisinier.Core.DTOs;

namespace Cuisinier.Infrastructure.Mappings;

public static class EntityMappings
{
    public static MenuResponse ToResponse(this Menu menu)
    {
        MenuParameters? parameters = null;
        if (!string.IsNullOrEmpty(menu.GenerationParametersJson))
        {
            try
            {
                parameters = JsonSerializer.Deserialize<MenuParameters>(menu.GenerationParametersJson);
            }
            catch
            {
                // If deserialization fails, leave parameters as null
            }
        }

        return new MenuResponse
        {
            Id = menu.Id,
            WeekStartDate = menu.WeekStartDate,
            CreationDate = menu.CreationDate,
            GenerationParameters = parameters,
            Recipes = menu.Recipes
                .OrderBy(r => r.Servings)
                .Select(r => r.ToResponse())
                .ToList()
        };
    }

    public static RecipeResponse ToResponse(this Recipe recipe)
    {
        return new RecipeResponse
        {
            Id = recipe.Id,
            MenuId = recipe.MenuId,
            Title = recipe.Title,
            Description = recipe.Description,
            CompleteDescription = recipe.CompleteDescription,
            DetailedRecipe = recipe.DetailedRecipe,
            ImageUrl = recipe.ImageUrl,
            PreparationTime = recipe.PreparationTime,
            CookingTime = recipe.CookingTime,
            Kcal = recipe.Kcal,
            Servings = recipe.Servings,
            Ingredients = recipe.Ingredients.Select(i => i.ToResponse()).ToList(),
            IsFromDatabase = recipe.IsFromDatabase,
            OriginalDishId = recipe.OriginalDishId
        };
    }

    public static IngredientResponse ToResponse(this RecipeIngredient ingredient)
    {
        return new IngredientResponse
        {
            Id = ingredient.Id,
            Name = ingredient.Name,
            Quantity = ingredient.Quantity,
            Category = ingredient.Category
        };
    }

    public static ShoppingListResponse ToResponse(this ShoppingList shoppingList)
    {
        return new ShoppingListResponse
        {
            Id = shoppingList.Id,
            MenuId = shoppingList.MenuId,
            CreationDate = shoppingList.CreationDate,
            Items = shoppingList.Items.Select(i => i.ToResponse()).ToList()
        };
    }

    public static ShoppingListItemResponse ToResponse(this ShoppingListItem item)
    {
        return new ShoppingListItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            Quantity = item.Quantity,
            Category = item.Category,
            IsManuallyAdded = item.IsManuallyAdded
        };
    }

    public static Recipe ToEntity(this RecipeResponse recipeResponse, int menuId)
    {
        var recipe = new Recipe
        {
            MenuId = menuId,
            Title = recipeResponse.Title,
            Description = recipeResponse.Description,
            CompleteDescription = recipeResponse.CompleteDescription,
            DetailedRecipe = recipeResponse.DetailedRecipe,
            ImageUrl = recipeResponse.ImageUrl,
            PreparationTime = recipeResponse.PreparationTime,
            CookingTime = recipeResponse.CookingTime,
            Kcal = recipeResponse.Kcal,
            Servings = recipeResponse.Servings,
            IsFromDatabase = recipeResponse.IsFromDatabase,
            OriginalDishId = recipeResponse.OriginalDishId,
            Ingredients = recipeResponse.Ingredients.Select(i => new RecipeIngredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };

        if (recipeResponse.Id > 0)
        {
            recipe.Id = recipeResponse.Id;
        }

        return recipe;
    }
}

