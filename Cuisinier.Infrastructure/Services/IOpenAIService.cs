using Cuisinier.Shared.DTOs;

namespace Cuisinier.Infrastructure.Services;

public interface IOpenAIService
{
    Task<MenuResponse> GenerateMenuAsync(MenuParameters parameters);
    Task<string> GenerateDetailedRecipeAsync(string recipeTitle, List<IngredientResponse> ingredients, string shortDescription);
    Task<RecipeResponse> ReplaceRecipeAsync(MenuParameters parameters, RecipeResponse recipeToReplace);
    Task<ShoppingListResponse> GenerateShoppingListAsync(List<RecipeResponse> recipes);
}

