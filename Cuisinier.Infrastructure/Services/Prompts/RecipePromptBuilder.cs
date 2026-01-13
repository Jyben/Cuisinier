using Cuisinier.Shared.DTOs;

namespace Cuisinier.Infrastructure.Services.Prompts;

public class RecipePromptBuilder : IPromptBuilder
{
    private readonly string _recipeTitle;
    private readonly List<IngredientResponse> _ingredients;
    private readonly string _shortDescription;

    public RecipePromptBuilder(string recipeTitle, List<IngredientResponse> ingredients, string shortDescription)
    {
        _recipeTitle = recipeTitle;
        _ingredients = ingredients;
        _shortDescription = shortDescription;
    }

    public string Build()
    {
        var ingredientsList = string.Join("\n", _ingredients.Select(i => $"- {i.Name}: {i.Quantity}"));
        
        return $@"Génère une recette complète et détaillée pour le plat suivant :

Titre : {_recipeTitle}
Description : {_shortDescription}

Ingrédients disponibles (liste COMPLÈTE et OBLIGATOIRE à utiliser) :
{ingredientsList}

Génère une recette détaillée avec :
1. Une introduction (2-3 phrases)
2. Les étapes de préparation numérotées et détaillées
3. Des conseils de cuisson si nécessaire
4. Des suggestions de présentation

IMPORTANT - CONTRAINTES OBLIGATOIRES : 
- N'inclus PAS le titre du plat car il est déjà affiché ailleurs.
- N'inclus PAS la liste des ingrédients car elle est déjà affichée ailleurs.
- Commence directement par l'introduction sans répéter le titre.
- Tu DOIS utiliser UNIQUEMENT les ingrédients listés ci-dessus. N'ajoute AUCUN ingrédient qui ne figure pas dans cette liste.
- La recette doit être cohérente avec les ingrédients fournis. Si un ingrédient est mentionné dans la liste, il DOIT être utilisé dans les étapes de préparation.

Rédige la recette de manière claire, pédagogique et appétissante. Utilise un ton chaleureux et convivial. 
Formate la réponse en Markdown avec des titres (##, ###), des listes à puces (-) et des listes numérotées (1., 2., etc.).";
    }
}
