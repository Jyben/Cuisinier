using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cuisinier.Core.Entities;
using Cuisinier.Infrastructure.Data;

namespace Cuisinier.Infrastructure.Services;

public class RecipeService : IRecipeService
{
    private readonly CuisinierDbContext _context;
    private readonly ILogger<RecipeService> _logger;

    public RecipeService(CuisinierDbContext context, ILogger<RecipeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Recipe> CopyModifiedRecipeAsync(Recipe originalRecipe, string newName)
    {
        var newRecipe = new Recipe
        {
            MenuId = originalRecipe.MenuId,
            Title = newName,
            Description = originalRecipe.Description,
            CompleteDescription = originalRecipe.CompleteDescription,
            ImageUrl = originalRecipe.ImageUrl,
            PreparationTime = originalRecipe.PreparationTime,
            CookingTime = originalRecipe.CookingTime,
            Servings = originalRecipe.Servings,
            IsFromDatabase = false,
            OriginalDishId = originalRecipe.Id,
            Ingredients = originalRecipe.Ingredients.Select(i => new RecipeIngredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };

        _context.Recipes.Add(newRecipe);
        await _context.SaveChangesAsync();

        return newRecipe;
    }

    public async Task<Recipe> ReuseRecipeAsync(Recipe recipe, int menuId)
    {
        var newRecipe = new Recipe
        {
            MenuId = menuId,
            Title = recipe.Title,
            Description = recipe.Description,
            CompleteDescription = recipe.CompleteDescription,
            ImageUrl = recipe.ImageUrl,
            PreparationTime = recipe.PreparationTime,
            CookingTime = recipe.CookingTime,
            Servings = recipe.Servings,
            IsFromDatabase = true,
            OriginalDishId = recipe.IsFromDatabase ? recipe.OriginalDishId ?? recipe.Id : recipe.Id,
            Ingredients = recipe.Ingredients.Select(i => new RecipeIngredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Category = i.Category
            }).ToList()
        };

        _context.Recipes.Add(newRecipe);
        await _context.SaveChangesAsync();

        return newRecipe;
    }
}

