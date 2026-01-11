namespace Cuisinier.Shared.DTOs;

public class RecipeResponse
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CompleteDescription { get; set; }
    public string? DetailedRecipe { get; set; }
    public string? ImageUrl { get; set; }
    public TimeSpan? PreparationTime { get; set; }
    public TimeSpan? CookingTime { get; set; }
    public int? Kcal { get; set; }
    public int Servings { get; set; }
    public List<IngredientResponse> Ingredients { get; set; } = new();
    public bool IsFromDatabase { get; set; }
    public int? OriginalDishId { get; set; }
}

