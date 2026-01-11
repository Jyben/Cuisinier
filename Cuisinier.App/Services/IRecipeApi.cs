using Cuisinier.Shared.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IRecipeApi
{
    [Get("/api/recipe")]
    Task<List<RecipeResponse>> GetAllRecipesAsync();
    
    [Get("/api/recipe/{id}")]
    Task<RecipeResponse> GetRecipeAsync(int id);
    
    [Post("/api/recipe/{id}/reuse")]
    Task<RecipeResponse> ReuseRecipeAsync(int id, [Body] ReuseRecipeRequest request);
}

