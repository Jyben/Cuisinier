using Cuisinier.Core.DTOs;

namespace Cuisinier.Api.Services;

public interface IRecipeQueryService
{
    Task<List<RecipeResponse>> GetAllRecipesAsync();
    Task<RecipeResponse?> GetRecipeAsync(int id);
}

