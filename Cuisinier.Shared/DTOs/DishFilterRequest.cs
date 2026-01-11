namespace Cuisinier.Shared.DTOs;

public class DishFilterRequest
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public int? MinKcal { get; set; }
    public int? MaxKcal { get; set; }
    public TimeSpan? MaxPreparationTime { get; set; }
    public TimeSpan? MaxCookingTime { get; set; }
    public int? MinServings { get; set; }
    public int? MaxServings { get; set; }
    public string? IngredientName { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } // "title", "createdAt", "kcal", etc.
    public bool SortDescending { get; set; } = false;
}
