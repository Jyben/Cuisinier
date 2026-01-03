using Cuisinier.Core.DTOs;

namespace Cuisinier.Api.Services;

public interface IMenuService
{
    Task<MenuResponse> GenerateMenuAsync(MenuGenerationRequest request);
    Task<MenuResponse?> GetMenuAsync(int id);
    Task<List<MenuResponse>> GetAllMenusAsync();
    Task<MenuParameters?> GetLastParametersAsync();
    Task SaveParametersAsync(MenuParameters parameters);
    Task<RecipeResponse> ReplaceRecipeAsync(int menuId, int recipeId, RecipeReplacementRequest request);
    Task<RecipeResponse> ReplaceIngredientAsync(int menuId, int recipeId, IngredientReplacementRequest request);
    Task<RecipeResponse> AddFavoriteToMenuAsync(int menuId, int favoriteId);
    Task DeleteRecipeAsync(int menuId, int recipeId);
    Task DeleteMenuAsync(int menuId);
    Task<MenuResponse> ValidateMenuAsync(int menuId, ValidateMenuRequest? request = null);
}

