namespace Cuisinier.Core.Entities;

public class Menu
{
    public int Id { get; set; }
    public DateTime WeekStartDate { get; set; }
    public DateTime CreationDate { get; set; }
    public string? GenerationParametersJson { get; set; } // JSON of generation parameters (without the date)
    
    public List<Recipe> Recipes { get; set; } = new();
}

