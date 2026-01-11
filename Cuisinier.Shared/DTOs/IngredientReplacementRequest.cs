namespace Cuisinier.Shared.DTOs;

public class IngredientReplacementRequest
{
    public string IngredientToReplace { get; set; } = string.Empty;
    public string NewIngredient { get; set; } = string.Empty;
}

