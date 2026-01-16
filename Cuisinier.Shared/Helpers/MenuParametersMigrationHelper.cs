using Cuisinier.Shared.DTOs;

namespace Cuisinier.Shared.Helpers;

/// <summary>
/// Helper pour migrer les anciens paramètres de menu vers le nouveau format avec configurations par groupe
/// </summary>
public static class MenuParametersMigrationHelper
{
    /// <summary>
    /// Migre les anciens paramètres vers le nouveau format si nécessaire.
    /// Si les données sont déjà au nouveau format, retourne l'objet inchangé.
    /// </summary>
    public static MenuParameters MigrateIfNeeded(MenuParameters parameters)
    {
        if (parameters == null)
        {
            return CreateDefaultParameters();
        }

        // Si déjà au nouveau format, retourner tel quel
        if (!parameters.IsLegacyFormat)
        {
            return parameters;
        }

        return MigrateFromLegacy(parameters);
    }

    /// <summary>
    /// Migre les anciens paramètres vers le nouveau format.
    /// </summary>
    private static MenuParameters MigrateFromLegacy(MenuParameters legacy)
    {
        var migrated = new MenuParameters
        {
            WeekStartDate = legacy.WeekStartDate,
            SeasonalFoods = legacy.SeasonalFoods,
            Configurations = new List<DishConfigurationDto>()
        };

        // Migrer chaque NumberOfDishes en configuration complète
#pragma warning disable CS0618 // Type or member is obsolete
        var legacyNumberOfDishes = legacy.NumberOfDishes ?? new List<NumberOfDishesDto>
        {
            new() { NumberOfDishes = 5, Servings = 2 }
        };

        foreach (var nod in legacyNumberOfDishes)
        {
            migrated.Configurations.Add(new DishConfigurationDto
            {
                NumberOfDishes = nod.NumberOfDishes,
                Servings = nod.Servings,
                Parameters = new DishConfigurationParametersDto
                {
                    DishTypes = legacy.DishTypes != null
                        ? new Dictionary<string, int?>(legacy.DishTypes)
                        : new Dictionary<string, int?>(),
                    BannedFoods = legacy.BannedFoods != null
                        ? new List<string>(legacy.BannedFoods)
                        : new List<string>(),
                    DesiredFoods = legacy.DesiredFoods != null
                        ? legacy.DesiredFoods.Select(f => new DesiredFoodDto
                        {
                            Food = f.Food,
                            Weight = f.Weight
                        }).ToList()
                        : new List<DesiredFoodDto>(),
                    WeightedOptions = legacy.WeightedOptions != null
                        ? new Dictionary<string, int?>(legacy.WeightedOptions)
                        : new Dictionary<string, int?>(),
                    MaxPreparationTime = legacy.MaxPreparationTime,
                    MaxCookingTime = legacy.MaxCookingTime,
                    MinKcalPerDish = legacy.MinKcalPerDish,
                    MaxKcalPerDish = legacy.MaxKcalPerDish
                }
            });
        }
#pragma warning restore CS0618

        return migrated;
    }

    /// <summary>
    /// Crée des paramètres par défaut au nouveau format
    /// </summary>
    public static MenuParameters CreateDefaultParameters()
    {
        return new MenuParameters
        {
            WeekStartDate = GetNextMonday(),
            SeasonalFoods = true,
            Configurations = new List<DishConfigurationDto>
            {
                new DishConfigurationDto
                {
                    NumberOfDishes = 5,
                    Servings = 2,
                    Parameters = new DishConfigurationParametersDto()
                }
            }
        };
    }

    /// <summary>
    /// Clone les paramètres d'une configuration pour en créer une nouvelle
    /// </summary>
    public static DishConfigurationParametersDto CloneParameters(DishConfigurationParametersDto source)
    {
        return new DishConfigurationParametersDto
        {
            DishTypes = new Dictionary<string, int?>(source.DishTypes),
            BannedFoods = new List<string>(source.BannedFoods),
            DesiredFoods = source.DesiredFoods.Select(f => new DesiredFoodDto
            {
                Food = f.Food,
                Weight = f.Weight
            }).ToList(),
            WeightedOptions = new Dictionary<string, int?>(source.WeightedOptions),
            MaxPreparationTime = source.MaxPreparationTime,
            MaxCookingTime = source.MaxCookingTime,
            MinKcalPerDish = source.MinKcalPerDish,
            MaxKcalPerDish = source.MaxKcalPerDish
        };
    }

    /// <summary>
    /// Copie les paramètres d'une configuration source vers une ou plusieurs configurations cibles
    /// </summary>
    public static void CopyParameters(
        DishConfigurationParametersDto source,
        DishConfigurationParametersDto target,
        bool copyDishTypes = true,
        bool copyBannedFoods = true,
        bool copyDesiredFoods = true,
        bool copyOptions = true,
        bool copyTime = true,
        bool copyNutrition = true)
    {
        if (copyDishTypes)
        {
            target.DishTypes = new Dictionary<string, int?>(source.DishTypes);
        }

        if (copyBannedFoods)
        {
            target.BannedFoods = new List<string>(source.BannedFoods);
        }

        if (copyDesiredFoods)
        {
            target.DesiredFoods = source.DesiredFoods.Select(f => new DesiredFoodDto
            {
                Food = f.Food,
                Weight = f.Weight
            }).ToList();
        }

        if (copyOptions)
        {
            target.WeightedOptions = new Dictionary<string, int?>(source.WeightedOptions);
        }

        if (copyTime)
        {
            target.MaxPreparationTime = source.MaxPreparationTime;
            target.MaxCookingTime = source.MaxCookingTime;
        }

        if (copyNutrition)
        {
            target.MinKcalPerDish = source.MinKcalPerDish;
            target.MaxKcalPerDish = source.MaxKcalPerDish;
        }
    }

    private static DateTime GetNextMonday()
    {
        var today = DateTime.Today;
        int daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0 && today.DayOfWeek != DayOfWeek.Monday)
        {
            daysUntilMonday = 7;
        }
        return today.AddDays(daysUntilMonday);
    }
}
