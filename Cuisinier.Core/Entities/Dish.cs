namespace Cuisinier.Core.Entities;

public class Dish
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CompleteDescription { get; set; }
    public string? DetailedRecipe { get; set; }
    public string? ImageUrl { get; set; }
    public TimeSpan? PreparationTime { get; set; }
    public TimeSpan? CookingTime { get; set; }
    public int? Kcal { get; set; }
    public int Servings { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public List<DishIngredient> Ingredients { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
    public List<ShoppingListDish> ShoppingListDishes { get; set; } = new();
}
