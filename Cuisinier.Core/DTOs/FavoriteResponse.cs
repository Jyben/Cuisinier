namespace Cuisinier.Core.DTOs;

public class FavoriteResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CompleteDescription { get; set; }
    public string? DetailedRecipe { get; set; }
    public string? ImageUrl { get; set; }
    public TimeSpan? PreparationTime { get; set; }
    public TimeSpan? CookingTime { get; set; }
    public int? Kcal { get; set; }
    public int Servings { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<IngredientResponse> Ingredients { get; set; } = new();
}
