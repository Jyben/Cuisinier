namespace Cuisinier.Core.DTOs;

public class MenuGenerationRequest
{
    public MenuParameters Parameters { get; set; } = new();
    public List<int>? RecipeIds { get; set; } // To reuse existing recipes
}

