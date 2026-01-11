namespace Cuisinier.Shared.DTOs;

public class CheckDuplicatesBatchRequest
{
    public List<RecipeCheckItem> Recipes { get; set; } = new();
}

public class RecipeCheckItem
{
    public int RecipeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<IngredientRequest> Ingredients { get; set; } = new();
}
