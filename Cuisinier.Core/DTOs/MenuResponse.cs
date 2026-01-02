namespace Cuisinier.Core.DTOs;

public class MenuResponse
{
    public int Id { get; set; }
    public DateTime WeekStartDate { get; set; }
    public DateTime CreationDate { get; set; }
    public MenuParameters? GenerationParameters { get; set; } // Parameters used to generate the menu (without the date)
    public List<RecipeResponse> Recipes { get; set; } = new();
}

