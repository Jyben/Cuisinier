namespace Cuisinier.Infrastructure.Services.Options;

public class OpenAIServiceOptions
{
    public const string SectionName = "OpenAI";

    public string Model { get; set; } = "gpt-4o-mini";
    public int MaxDishesPerBatch { get; set; } = 8;
    
    public TemperatureSettings Temperatures { get; set; } = new();
    
    public class TemperatureSettings
    {
        public float Menu { get; set; } = 0.7f;
        public float DetailedRecipe { get; set; } = 0.8f;
        public float RecipeReplacement { get; set; } = 0.8f;
        public float ShoppingList { get; set; } = 0.3f;
    }
}
