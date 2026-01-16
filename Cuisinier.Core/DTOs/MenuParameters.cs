using System.Text.Json.Serialization;

namespace Cuisinier.Core.DTOs;

public class MenuParameters
{
    // === Paramètres globaux (conservés) ===
    public DateTime WeekStartDate { get; set; }
    public bool SeasonalFoods { get; set; } = true;

    // === Nouveau format : configurations avec paramètres par groupe ===
    public List<DishConfigurationDto> Configurations { get; set; } = new();

    // === Ancien format (conservé pour rétrocompatibilité de désérialisation) ===
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations à la place. Conservé uniquement pour migration des anciennes données.")]
    public List<NumberOfDishesDto>? NumberOfDishes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.DishTypes à la place.")]
    public Dictionary<string, int?>? DishTypes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.BannedFoods à la place.")]
    public List<string>? BannedFoods { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.DesiredFoods à la place.")]
    public List<DesiredFoodDto>? DesiredFoods { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.WeightedOptions à la place.")]
    public Dictionary<string, int?>? WeightedOptions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.MaxPreparationTime à la place.")]
    public TimeSpan? MaxPreparationTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.MaxCookingTime à la place.")]
    public TimeSpan? MaxCookingTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.MinKcalPerDish à la place.")]
    public int? MinKcalPerDish { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("Utiliser Configurations[].Parameters.MaxKcalPerDish à la place.")]
    public int? MaxKcalPerDish { get; set; }

    /// <summary>
    /// Vérifie si les données sont au nouveau format (Configurations) ou à l'ancien format
    /// </summary>
    [JsonIgnore]
#pragma warning disable CS0618 // Access to obsolete member is intentional for migration check
    public bool IsLegacyFormat => Configurations.Count == 0 && NumberOfDishes?.Count > 0;
#pragma warning restore CS0618
}

