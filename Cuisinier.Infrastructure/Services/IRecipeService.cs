using Cuisinier.Shared.DTOs;
using Cuisinier.Core.Entities;

namespace Cuisinier.Infrastructure.Services;

public interface IRecipeService
{
    Task<Recipe> CopyModifiedRecipeAsync(Recipe originalRecipe, string newName);
    Task<Recipe> ReuseRecipeAsync(Recipe recipe, int menuId);
}

