namespace Cuisinier.Infrastructure.Services.DTOs;

// DTOs for deserialization from OpenAI responses
// Note: Property names must remain in French to match JSON keys from OpenAI responses
public class MenuResponseDto
{
    public List<RecipeResponseDto> Recettes { get; set; } = new();
}

public class RecipeResponseDto
{
    public string Titre { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? TempsPreparation { get; set; }
    public string? TempsCuisson { get; set; }
    public int? Kcal { get; set; }
    public int Personnes { get; set; }
    public List<IngredientResponseDto> Ingredients { get; set; } = new();
}

public class IngredientResponseDto
{
    public string Nom { get; set; } = string.Empty;
    public string? Quantite { get; set; }
    public string? Categorie { get; set; }
}

public class ShoppingListResponseDto
{
    public List<ShoppingListItemResponseDto> Items { get; set; } = new();
}

public class ShoppingListItemResponseDto
{
    public string Nom { get; set; } = string.Empty;
    public string? Quantite { get; set; }
    public string? Categorie { get; set; }
}
