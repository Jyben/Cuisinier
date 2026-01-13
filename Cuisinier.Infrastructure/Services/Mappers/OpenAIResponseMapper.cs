using Cuisinier.Shared.DTOs;
using Cuisinier.Infrastructure.Services.DTOs;
using Cuisinier.Infrastructure.Services.Helpers;

namespace Cuisinier.Infrastructure.Services.Mappers;

public class OpenAIResponseMapper
{
    private readonly TimeSpanParser _timeSpanParser;

    public OpenAIResponseMapper(TimeSpanParser timeSpanParser)
    {
        _timeSpanParser = timeSpanParser;
    }

    public MenuResponse MapToMenuResponse(MenuResponseDto menuData, DateTime weekStartDate)
    {
        return new MenuResponse
        {
            WeekStartDate = weekStartDate,
            CreationDate = DateTime.UtcNow,
            Recipes = menuData.Recettes.Select(r => MapToRecipeResponse(r)).ToList()
        };
    }

    public RecipeResponse MapToRecipeResponse(RecipeResponseDto recipeData)
    {
        return new RecipeResponse
        {
            Title = recipeData.Titre,
            Description = recipeData.Description,
            PreparationTime = _timeSpanParser.Parse(recipeData.TempsPreparation),
            CookingTime = _timeSpanParser.Parse(recipeData.TempsCuisson),
            Kcal = recipeData.Kcal,
            Servings = recipeData.Personnes,
            Ingredients = recipeData.Ingredients.Select(i => MapToIngredientResponse(i)).ToList()
        };
    }

    public ShoppingListResponse MapToShoppingListResponse(ShoppingListResponseDto listData, int menuId = 0)
    {
        return new ShoppingListResponse
        {
            MenuId = menuId,
            CreationDate = DateTime.UtcNow,
            Items = listData.Items.Select(i => MapToShoppingListItemResponse(i)).ToList()
        };
    }

    private IngredientResponse MapToIngredientResponse(IngredientResponseDto ingredientData)
    {
        return new IngredientResponse
        {
            Name = ingredientData.Nom,
            Quantity = ingredientData.Quantite ?? "",
            Category = ingredientData.Categorie ?? ""
        };
    }

    private ShoppingListItemResponse MapToShoppingListItemResponse(ShoppingListItemResponseDto itemData)
    {
        return new ShoppingListItemResponse
        {
            Name = itemData.Nom,
            Quantity = itemData.Quantite ?? "",
            Category = itemData.Categorie ?? "",
            IsManuallyAdded = false
        };
    }
}
