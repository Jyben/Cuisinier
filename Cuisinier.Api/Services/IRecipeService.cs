using Cuisinier.Shared.DTOs;

namespace Cuisinier.Api.Services;

public interface IRecipeQueryService
{
    Task<List<RecipeResponse>> GetAllRecipesAsync(string userId);
    Task<RecipeResponse?> GetRecipeAsync(int id, string userId);
}

