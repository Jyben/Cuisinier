namespace Cuisinier.Core.DTOs;

public class MenuParameters
{
    public List<NumberOfDishesDto> NumberOfDishes { get; set; } = new();
    public Dictionary<string, int?> DishTypes { get; set; } = new(); // null = optional type without specific number, > 0 = number of dishes of this type
    public List<string> BannedFoods { get; set; } = new();
    public List<DesiredFoodDto> DesiredFoods { get; set; } = new();
    public bool SeasonalFoods { get; set; } = true;
    public Dictionary<string, int?> WeightedOptions { get; set; } = new(); // null = option not activated, 0 = none (0%), > 0 = percentage
    public TimeSpan? MaxPreparationTime { get; set; }
    public TimeSpan? MaxCookingTime { get; set; }
    public int? TotalKcalPerDish { get; set; } // Total number of kcal per dish (optional)
    public DateTime WeekStartDate { get; set; }
}

