using Cuisinier.Shared.DTOs;

namespace Cuisinier.Api.Services;

public interface IMenuService
{
    Task<MenuResponse> GenerateMenuAsync(MenuGenerationRequest request, string userId);
    Task<int> StartMenuGenerationAsync(MenuGenerationRequest request, string userId);
    Task<MenuResponse?> GetMenuAsync(int id, string userId);
    Task<List<MenuResponse>> GetAllMenusAsync(string userId);
    Task<MenuParameters?> GetLastParametersAsync(string userId);
    Task SaveParametersAsync(MenuParameters parameters, string userId);
    Task<RecipeResponse> ReplaceRecipeAsync(int menuId, int recipeId, RecipeReplacementRequest request, string userId);
    Task<RecipeResponse> ReplaceIngredientAsync(int menuId, int recipeId, IngredientReplacementRequest request, string userId);
    Task<RecipeResponse> AddFavoriteToMenuAsync(int menuId, int favoriteId, string userId);
    Task DeleteRecipeAsync(int menuId, int recipeId, string userId);
    Task<RecipeResponse> ToggleRecipeCookedStatusAsync(int menuId, int recipeId, string userId);
    Task DeleteMenuAsync(int menuId, string userId);
    Task<MenuResponse> ValidateMenuAsync(int menuId, ValidateMenuRequest? request, string userId);
}

