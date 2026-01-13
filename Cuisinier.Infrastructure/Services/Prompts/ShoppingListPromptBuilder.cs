using Cuisinier.Shared.DTOs;

namespace Cuisinier.Infrastructure.Services.Prompts;

public class ShoppingListPromptBuilder : IPromptBuilder
{
    private readonly List<RecipeResponse> _recipes;

    public ShoppingListPromptBuilder(List<RecipeResponse> recipes)
    {
        _recipes = recipes;
    }

    public string Build()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Génère une liste de courses organisée par catégories à partir des recettes suivantes :\n");

        foreach (var recipe in _recipes)
        {
            sb.AppendLine($"\n{recipe.Title} ({recipe.Servings} personnes):");
            foreach (var ingredient in recipe.Ingredients)
            {
                sb.AppendLine($"- {ingredient.Name}: {ingredient.Quantity}");
            }
        }

        sb.AppendLine("\n\nGénère une liste de courses organisée par catégories (Légumes, Fruits, Viandes, Poissons, Produits laitiers, Épicerie, etc.) en regroupant les ingrédients similaires et en additionnant les quantités quand c'est pertinent.");
        sb.AppendLine("\nRéponds en JSON avec cette structure :");
        sb.AppendLine(@"{
  ""items"": [
    {
      ""nom"": ""Nom de l'ingrédient"",
      ""quantite"": ""Quantité totale"",
      ""categorie"": ""Catégorie""
    }
  ]
}");

        return sb.ToString();
    }
}
