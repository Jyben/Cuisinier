namespace Cuisinier.Core.DTOs;

/// <summary>
/// Représente une configuration complète pour un groupe de plats
/// (ex: 5 plats pour 2 personnes avec leurs paramètres spécifiques)
/// </summary>
public class DishConfigurationDto
{
    /// <summary>Identifiant unique de la configuration (pour la gestion UI)</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nombre de plats à générer pour cette configuration</summary>
    public int NumberOfDishes { get; set; } = 5;

    /// <summary>Nombre de personnes pour cette configuration</summary>
    public int Servings { get; set; } = 2;

    /// <summary>Nom optionnel pour identifier la configuration (ex: "Semaine", "Week-end")</summary>
    public string? Name { get; set; }

    /// <summary>Paramètres spécifiques à cette configuration</summary>
    public DishConfigurationParametersDto Parameters { get; set; } = new();
}

/// <summary>
/// Paramètres détaillés pour une configuration de plats
/// </summary>
public class DishConfigurationParametersDto
{
    /// <summary>Types de plats avec nombres optionnels (null = optionnel, >0 = nombre spécifique)</summary>
    public Dictionary<string, int?> DishTypes { get; set; } = new();

    /// <summary>Aliments interdits pour cette configuration</summary>
    public List<string> BannedFoods { get; set; } = new();

    /// <summary>Aliments souhaités pour cette configuration</summary>
    public List<DesiredFoodDto> DesiredFoods { get; set; } = new();

    /// <summary>Options pondérées (Équilibré, Gourmand, Végan, etc.)</summary>
    public Dictionary<string, int?> WeightedOptions { get; set; } = new();

    /// <summary>Temps de préparation maximum (null = pas de limite)</summary>
    public TimeSpan? MaxPreparationTime { get; set; }

    /// <summary>Temps de cuisson maximum (null = pas de limite)</summary>
    public TimeSpan? MaxCookingTime { get; set; }

    /// <summary>Calories minimum par plat (null = pas de limite)</summary>
    public int? MinKcalPerDish { get; set; }

    /// <summary>Calories maximum par plat (null = pas de limite)</summary>
    public int? MaxKcalPerDish { get; set; }
}
