namespace Cuisinier.Core.DTOs;

public class FavoriteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CompleteDescription { get; set; }
    public string? DetailedRecipe { get; set; }
    public string? ImageUrl { get; set; }
    public TimeSpan? PreparationTime { get; set; }
    public TimeSpan? CookingTime { get; set; }
    public int? Kcal { get; set; }
    public int Servings { get; set; }
    public List<IngredientRequest> Ingredients { get; set; } = new();
}

public class IngredientRequest
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
