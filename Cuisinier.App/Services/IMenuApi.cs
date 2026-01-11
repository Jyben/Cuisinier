using Cuisinier.Shared.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IMenuApi
{
    [Post("/api/menu/generate")]
    Task<MenuGenerationResponse> GenerateMenuAsync([Body] MenuGenerationRequest request);
    
    [Get("/api/menu/{id}")]
    Task<MenuResponse> GetMenuAsync(int id);
    
    [Get("/api/menu/last-parameters")]
    Task<MenuParameters?> GetLastParametersAsync();

    [Post("/api/menu/parameters")]
    Task SaveParametersAsync([Body] MenuParameters parameters);
    
    [Get("/api/menu")]
    Task<List<MenuResponse>> GetAllMenusAsync();
    
    [Post("/api/menu/{menuId}/recipe/{recipeId}/replace")]
    Task<RecipeResponse> ReplaceRecipeAsync(int menuId, int recipeId, [Body] RecipeReplacementRequest request);
    
    [Post("/api/menu/{menuId}/recipe/{recipeId}/ingredient/replace")]
    Task<RecipeResponse> ReplaceIngredientAsync(int menuId, int recipeId, [Body] IngredientReplacementRequest request);
    
    [Post("/api/menu/{menuId}/favorite/{favoriteId}")]
    Task<RecipeResponse> AddFavoriteToMenuAsync(int menuId, int favoriteId);
    
    [Delete("/api/menu/{menuId}/recipe/{recipeId}")]
    Task DeleteRecipeAsync(int menuId, int recipeId);
    
    [Post("/api/menu/{menuId}/recipe/{recipeId}/toggle-cooked")]
    Task<RecipeResponse> ToggleRecipeCookedStatusAsync(int menuId, int recipeId);
    
    [Delete("/api/menu/{menuId}")]
    Task DeleteMenuAsync(int menuId);
    
    [Post("/api/menu/{menuId}/validate")]
    Task<MenuResponse> ValidateMenuAsync(int menuId, [Body] ValidateMenuRequest? request = null);
}

