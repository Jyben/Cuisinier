using Cuisinier.Shared.DTOs;
using Cuisinier.Infrastructure.Services.Prompts.Components;

namespace Cuisinier.Infrastructure.Services.Prompts;

public class RecipeReplacementPromptBuilder : IPromptBuilder
{
    private string? _recipeTitle;
    private string? _recipeDescription;
    private DateTime? _weekStartDate;
    private bool _seasonalFoods;
    private int? _minKcalPerDish;
    private int? _maxKcalPerDish;
    private List<string>? _bannedFoods;
    private TimeSpan? _maxPreparationTime;
    private TimeSpan? _maxCookingTime;

    private const string JsonStructure = @"{
  ""titre"": ""Titre du plat"",
  ""description"": ""Description courte"",
  ""tempsPreparation"": ""00:30:00"",
  ""tempsCuisson"": ""01:00:00"",
  ""kcal"": 450,
  ""personnes"": 4,
  ""ingredients"": [
    {
      ""nom"": ""Nom de l'ingrédient"",
      ""quantite"": ""200g"",
      ""categorie"": ""Légumes""
    }
  ]
}";

    public RecipeReplacementPromptBuilder WithRecipeToReplace(string title, string description)
    {
        _recipeTitle = title;
        _recipeDescription = description;
        return this;
    }

    public RecipeReplacementPromptBuilder WithWeekStartDate(DateTime weekStartDate)
    {
        _weekStartDate = weekStartDate;
        return this;
    }

    public RecipeReplacementPromptBuilder WithSeasonalFoods(bool enabled, DateTime weekStartDate)
    {
        _seasonalFoods = enabled;
        _weekStartDate = weekStartDate;
        return this;
    }

    public RecipeReplacementPromptBuilder WithCalorieConstraints(int? minKcal, int? maxKcal)
    {
        _minKcalPerDish = minKcal;
        _maxKcalPerDish = maxKcal;
        return this;
    }

    public RecipeReplacementPromptBuilder WithBannedFoods(List<string> bannedFoods)
    {
        _bannedFoods = bannedFoods;
        return this;
    }

    public RecipeReplacementPromptBuilder WithMaxPreparationTime(TimeSpan? maxPreparationTime)
    {
        _maxPreparationTime = maxPreparationTime;
        return this;
    }

    public RecipeReplacementPromptBuilder WithMaxCookingTime(TimeSpan? maxCookingTime)
    {
        _maxCookingTime = maxCookingTime;
        return this;
    }

    public string Build()
    {
        if (string.IsNullOrEmpty(_recipeTitle))
            throw new InvalidOperationException("Recipe title is required");

        var sb = new System.Text.StringBuilder();
        
        AppendRecipeToReplace(sb);
        AppendTimeConstraints(sb);
        AppendCalorieConstraints(sb);
        AppendBannedFoods(sb);
        AppendSeasonalConstraints(sb);
        AppendSingleDishInstruction(sb);
        AppendJsonStructure(sb);
        AppendMandatoryConstraints(sb);
        
        return sb.ToString();
    }

    private void AppendRecipeToReplace(System.Text.StringBuilder sb)
    {
        sb.AppendLine($"L'utilisateur ne veut PAS de ce plat: \"{_recipeTitle}\"");
        if (!string.IsNullOrEmpty(_recipeDescription))
        {
            sb.AppendLine($"Description du plat refusé: {_recipeDescription}");
        }
        sb.AppendLine("\nTu DOIS proposer un plat COMPLÈTEMENT DIFFÉRENT:");
        sb.AppendLine("- PAS de variante du même plat (si c'était un gratin, ne propose PAS un autre gratin)");
        sb.AppendLine("- PAS le même type de cuisson (si c'était mijoté, propose autre chose)");
        sb.AppendLine("- PAS les mêmes ingrédients principaux (si c'était du poulet, propose du poisson ou autre chose)");
        sb.AppendLine("- Propose un plat avec une base et une technique de cuisson différentes");
    }

    private void AppendTimeConstraints(System.Text.StringBuilder sb)
    {
        if (_maxPreparationTime.HasValue)
        {
            sb.AppendLine($"Temps de préparation maximum: {_maxPreparationTime.Value.TotalMinutes} minutes");
        }

        if (_maxCookingTime.HasValue)
        {
            sb.AppendLine($"Temps de cuisson maximum: {_maxCookingTime.Value.TotalMinutes} minutes");
        }
    }

    private void AppendCalorieConstraints(System.Text.StringBuilder sb)
    {
        if (!_minKcalPerDish.HasValue && !_maxKcalPerDish.HasValue)
            return;

        var calorieBuilder = new CalorieConstraintsBuilder(_minKcalPerDish, _maxKcalPerDish, false);
        var calorieConstraints = calorieBuilder.Build();
        if (!string.IsNullOrEmpty(calorieConstraints))
        {
            sb.Append(calorieConstraints);
        }
    }

    private void AppendBannedFoods(System.Text.StringBuilder sb)
    {
        if (_bannedFoods == null || !_bannedFoods.Any())
            return;

        sb.AppendLine($"Aliments à bannir: {string.Join(", ", _bannedFoods)}");
    }

    private void AppendSeasonalConstraints(System.Text.StringBuilder sb)
    {
        if (!_seasonalFoods || !_weekStartDate.HasValue)
            return;

        var seasonalBuilder = new SeasonalConstraintsBuilder(_weekStartDate.Value, _seasonalFoods);
        var seasonalConstraints = seasonalBuilder.Build();
        if (!string.IsNullOrEmpty(seasonalConstraints))
        {
            sb.Append(seasonalConstraints);
        }
    }

    private void AppendSingleDishInstruction(System.Text.StringBuilder sb)
    {
        sb.AppendLine("\nIMPORTANT: Tu DOIS générer UNIQUEMENT UN SEUL plat (pas un tableau, pas plusieurs plats).");
    }

    private void AppendJsonStructure(System.Text.StringBuilder sb)
    {
        sb.AppendLine("\nGénère une nouvelle recette TOTALEMENT DIFFÉRENTE, en JSON avec la structure suivante (un objet unique, pas un tableau) :");
        sb.AppendLine(JsonStructure);
        sb.AppendLine("\nIMPORTANT: Le champ \"kcal\" représente les calories PAR PERSONNE, pas pour le plat total. Si \"personnes\": 4 et \"kcal\": 450, cela signifie 450 kcal par personne (soit 1800 kcal pour le plat total de 4 personnes).");
    }

    private void AppendMandatoryConstraints(System.Text.StringBuilder sb)
    {
        sb.AppendLine("\nIMPORTANT - CONTRAINTES OBLIGATOIRES:");
        sb.AppendLine("- Tu DOIS générer UNIQUEMENT UN SEUL plat (un objet JSON unique, pas un tableau).");
        sb.AppendLine($"- Le plat proposé DOIT être COMPLÈTEMENT DIFFÉRENT de \"{_recipeTitle}\". Pas de variante, pas de plat similaire, pas le même type de préparation.");

        // Add time constraints as mandatory
        if (_maxPreparationTime.HasValue)
        {
            sb.AppendLine($"- Le temps de préparation (\"tempsPreparation\") DOIT être INFÉRIEUR OU ÉGAL à {_maxPreparationTime.Value.TotalMinutes} minutes. C'est une contrainte STRICTE.");
        }
        if (_maxCookingTime.HasValue)
        {
            sb.AppendLine($"- Le temps de cuisson (\"tempsCuisson\") DOIT être INFÉRIEUR OU ÉGAL à {_maxCookingTime.Value.TotalMinutes} minutes. C'est une contrainte STRICTE. Ne propose PAS de plats mijotés ou à cuisson longue si le temps maximum est court.");
        }

        sb.AppendLine("- Les quantités d'ingrédients DOIVENT être proportionnelles au nombre de personnes. Si tu génères un plat pour 1 personne, divise toutes les quantités par 4 par rapport à un plat pour 4 personnes.");
        sb.AppendLine("- Pour chaque recette, tu DOIS fournir le nombre de calories PAR PERSONNE (\"kcal\") dans le JSON. Calcule les calories de manière réaliste en fonction des ingrédients et de leurs quantités pour une seule personne. Un plat riche en viande, fromage ou crème doit avoir plus de calories qu'un plat principalement végétal.");
        sb.AppendLine("- Tu NE DOIS PAS générer de desserts (tartes, gâteaux, crèmes, glaces, fruits au sirop, etc.). Uniquement des plats principaux et entrées.");
        sb.AppendLine("- Tu DOIS fournir une liste COMPLÈTE et DÉTAILLÉE de TOUS les ingrédients nécessaires pour réaliser le plat. N'omets aucun ingrédient important.");
    }
}
