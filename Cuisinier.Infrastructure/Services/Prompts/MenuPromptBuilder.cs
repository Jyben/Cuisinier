using Cuisinier.Shared.DTOs;
using Cuisinier.Infrastructure.Services.Prompts.Components;

namespace Cuisinier.Infrastructure.Services.Prompts;

public class MenuPromptBuilder : IPromptBuilder
{
    private DateTime? _weekStartDate;
    private List<NumberOfDishesDto>? _dishesToGenerate;
    private List<string>? _alreadyGeneratedTitles;
    private Dictionary<string, int?>? _dishTypes;
    private List<string>? _bannedFoods;
    private List<DesiredFoodDto>? _desiredFoods;
    private bool _seasonalFoods;
    private int? _minKcalPerDish;
    private int? _maxKcalPerDish;
    private Dictionary<string, int?>? _weightedOptions;
    private TimeSpan? _maxPreparationTime;
    private TimeSpan? _maxCookingTime;

    private const string JsonStructure = @"{
  ""recettes"": [
    {
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
    }
  ]
}";

    public MenuPromptBuilder WithWeekStartDate(DateTime weekStartDate)
    {
        _weekStartDate = weekStartDate;
        return this;
    }

    public MenuPromptBuilder WithDishCounts(List<NumberOfDishesDto> dishesToGenerate)
    {
        _dishesToGenerate = dishesToGenerate;
        return this;
    }

    public MenuPromptBuilder WithAlreadyGeneratedTitles(List<string> alreadyGeneratedTitles)
    {
        _alreadyGeneratedTitles = alreadyGeneratedTitles;
        return this;
    }

    public MenuPromptBuilder WithDishTypes(Dictionary<string, int?> dishTypes)
    {
        _dishTypes = dishTypes;
        return this;
    }

    public MenuPromptBuilder WithBannedFoods(List<string> bannedFoods)
    {
        _bannedFoods = bannedFoods;
        return this;
    }

    public MenuPromptBuilder WithDesiredFoods(List<DesiredFoodDto> desiredFoods)
    {
        _desiredFoods = desiredFoods;
        return this;
    }

    public MenuPromptBuilder WithSeasonalFoods(bool enabled, DateTime weekStartDate)
    {
        _seasonalFoods = enabled;
        _weekStartDate = weekStartDate;
        return this;
    }

    public MenuPromptBuilder WithCalorieConstraints(int? minKcal, int? maxKcal)
    {
        _minKcalPerDish = minKcal;
        _maxKcalPerDish = maxKcal;
        return this;
    }

    public MenuPromptBuilder WithDietaryConstraints(Dictionary<string, int?> weightedOptions)
    {
        _weightedOptions = weightedOptions;
        return this;
    }

    public MenuPromptBuilder WithMaxPreparationTime(TimeSpan? maxPreparationTime)
    {
        _maxPreparationTime = maxPreparationTime;
        return this;
    }

    public MenuPromptBuilder WithMaxCookingTime(TimeSpan? maxCookingTime)
    {
        _maxCookingTime = maxCookingTime;
        return this;
    }

    public string Build()
    {
        if (!_weekStartDate.HasValue)
            throw new InvalidOperationException("WeekStartDate is required");

        var sb = new System.Text.StringBuilder();
        
        AppendHeader(sb);
        AppendDishCountInfo(sb);
        AppendAlreadyGeneratedTitles(sb);
        AppendDishCounts(sb);
        AppendDishTypes(sb);
        AppendBannedFoods(sb);
        AppendDesiredFoods(sb);
        AppendSeasonalConstraints(sb);
        AppendCalorieConstraints(sb, includeInConstraints: false);
        AppendDietaryConstraints(sb);
        AppendTimeConstraints(sb);
        AppendJsonStructure(sb);
        AppendMandatoryConstraints(sb);
        AppendStrictCalorieConstraints(sb);
        
        return sb.ToString();
    }

    private void AppendHeader(System.Text.StringBuilder sb)
    {
        sb.AppendLine("Génère un menu de la semaine avec les paramètres suivants:");
        sb.AppendLine($"Date de début de semaine: {_weekStartDate!.Value:yyyy-MM-dd}");
    }

    private void AppendDishCountInfo(System.Text.StringBuilder sb)
    {
        var totalDishes = _dishesToGenerate?.Sum(d => d.NumberOfDishes) ?? 0;
        if (totalDishes > 0)
        {
            sb.AppendLine($"\nIMPORTANT: Tu DOIS générer EXACTEMENT {totalDishes} plat(s) dans ce batch.");
        }
    }

    private void AppendAlreadyGeneratedTitles(System.Text.StringBuilder sb)
    {
        if (_alreadyGeneratedTitles == null || !_alreadyGeneratedTitles.Any())
            return;

        sb.AppendLine("\nPlats DÉJÀ générés (tu NE DOIS PAS les répéter, génère des plats DIFFÉRENTS):");
        foreach (var title in _alreadyGeneratedTitles)
        {
            sb.AppendLine($"- {title}");
        }
        sb.AppendLine("\nIMPORTANT: Génère des plats COMPLÈTEMENT DIFFÉRENTS de ceux listés ci-dessus. Ne répète aucun titre, aucune variante similaire.");
    }

    private void AppendDishCounts(System.Text.StringBuilder sb)
    {
        if (_dishesToGenerate == null || !_dishesToGenerate.Any())
            return;

        sb.AppendLine("\nNombre de plats à générer:");
        foreach (var item in _dishesToGenerate)
        {
            sb.AppendLine($"- {item.NumberOfDishes} plat(s), CHACUN pour {item.Servings} personne(s)");
        }
        sb.AppendLine("\nIMPORTANT: Chaque plat généré doit avoir le nombre de personnes (\"personnes\") correspondant à celui spécifié ci-dessus. Si tu génères 4 plats pour 1 personne, alors CHAQUE plat doit avoir \"personnes\": 1 dans le JSON.");
    }

    private void AppendDishTypes(System.Text.StringBuilder sb)
    {
        if (_dishTypes == null || !_dishTypes.Any())
            return;

        sb.AppendLine("\nTypes de plats souhaités:");
        foreach (var (type, number) in _dishTypes)
        {
            if (number.HasValue)
            {
                sb.AppendLine($"- {type}: {number.Value} fois");
            }
            else
            {
                sb.AppendLine($"- {type} (optionnel, peut être inclus dans le menu)");
            }
        }
    }

    private void AppendBannedFoods(System.Text.StringBuilder sb)
    {
        if (_bannedFoods == null || !_bannedFoods.Any())
            return;

        sb.AppendLine($"\nAliments à bannir: {string.Join(", ", _bannedFoods)}");
    }

    private void AppendDesiredFoods(System.Text.StringBuilder sb)
    {
        if (_desiredFoods == null || !_desiredFoods.Any())
            return;

        sb.AppendLine("\nAliments souhaités:");
        foreach (var item in _desiredFoods)
        {
            sb.AppendLine($"- {item.Food}");
        }
        sb.AppendLine("Ces aliments peuvent apparaître dans une ou plusieurs recettes du menu.");
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

    private void AppendCalorieConstraints(System.Text.StringBuilder sb, bool includeInConstraints)
    {
        if (!_minKcalPerDish.HasValue && !_maxKcalPerDish.HasValue)
            return;

        var calorieBuilder = new CalorieConstraintsBuilder(_minKcalPerDish, _maxKcalPerDish, includeInConstraints);
        var calorieConstraints = calorieBuilder.Build();
        if (!string.IsNullOrEmpty(calorieConstraints))
        {
            sb.Append(calorieConstraints);
        }
    }

    private void AppendDietaryConstraints(System.Text.StringBuilder sb)
    {
        if (_weightedOptions == null || !_weightedOptions.Any(kvp => kvp.Value.HasValue))
            return;

        var dietaryBuilder = new DietaryConstraintsBuilder(_weightedOptions);
        var dietaryConstraints = dietaryBuilder.Build();
        if (!string.IsNullOrEmpty(dietaryConstraints))
        {
            sb.Append(dietaryConstraints);
        }
    }

    private void AppendTimeConstraints(System.Text.StringBuilder sb)
    {
        if (_maxPreparationTime.HasValue)
        {
            sb.AppendLine($"\nTemps de préparation maximum: {_maxPreparationTime.Value.TotalMinutes} minutes");
        }

        if (_maxCookingTime.HasValue)
        {
            sb.AppendLine($"Temps de cuisson maximum: {_maxCookingTime.Value.TotalMinutes} minutes");
        }
    }

    private void AppendJsonStructure(System.Text.StringBuilder sb)
    {
        sb.AppendLine("\nGénère une réponse JSON avec cette structure:");
        sb.AppendLine(JsonStructure);
    }

    private void AppendMandatoryConstraints(System.Text.StringBuilder sb)
    {
        var totalDishes = _dishesToGenerate?.Sum(d => d.NumberOfDishes) ?? 0;
        
        sb.AppendLine($"\nIMPORTANT - CONTRAINTES OBLIGATOIRES:");
        if (totalDishes > 0)
        {
            sb.AppendLine($"- Le tableau 'recettes' DOIT contenir EXACTEMENT {totalDishes} recette(s). C'est une contrainte stricte et non négociable.");
        }
        
        // Add constraint about servings per dish
        if (_dishesToGenerate != null && _dishesToGenerate.Any())
        {
            // Group by servings to make it clearer
            var servingsGroups = _dishesToGenerate
                .GroupBy(d => d.Servings)
                .Select(g => new { Servings = g.Key, Count = g.Sum(d => d.NumberOfDishes) })
                .ToList();

            if (servingsGroups.Count == 1)
            {
                // Single servings value for all dishes
                var servings = servingsGroups[0].Servings;
                var count = servingsGroups[0].Count;
                sb.AppendLine($"- Le nombre de personnes (\"personnes\") dans CHAQUE recette DOIT être EXACTEMENT {servings}. Tu génères {count} plat(s), et CHAQUE plat doit avoir \"personnes\": {servings} dans le JSON.");
            }
            else
            {
                // Multiple servings values
                var servingsInfo = string.Join(", ", servingsGroups.Select(g => $"{g.Count} plat(s) pour {g.Servings} personne(s)"));
                sb.AppendLine($"- Le nombre de personnes (\"personnes\") dans chaque recette DOIT correspondre exactement aux spécifications: {servingsInfo}. Répartis correctement les plats selon le nombre de personnes requis pour chaque groupe.");
            }
        }
        
        sb.AppendLine("- Tu NE DOIS PAS générer de desserts (tartes, gâteaux, crèmes, glaces, fruits au sirop, etc.). Uniquement des plats principaux et entrées.");
        sb.AppendLine("- Pour chaque recette, tu DOIS fournir une liste COMPLÈTE et DÉTAILLÉE de TOUS les ingrédients nécessaires pour réaliser le plat. N'omets aucun ingrédient important (viande, poisson, légumes, épices, condiments, produits laitiers, etc.). La liste doit être exhaustive et réaliste.");
        sb.AppendLine("- Pour chaque recette, tu DOIS fournir le nombre total de calories (kcal) du plat. Calcule les calories en fonction des ingrédients et de leurs quantités.");
    }

    private void AppendStrictCalorieConstraints(System.Text.StringBuilder sb)
    {
        if (!_minKcalPerDish.HasValue && !_maxKcalPerDish.HasValue)
            return;

        var strictCalorieBuilder = new CalorieConstraintsBuilder(
            _minKcalPerDish, 
            _maxKcalPerDish, 
            includeInConstraints: true);
        var strictCalorieConstraints = strictCalorieBuilder.Build();
        if (!string.IsNullOrEmpty(strictCalorieConstraints))
        {
            sb.Append(strictCalorieConstraints);
        }
    }
}
