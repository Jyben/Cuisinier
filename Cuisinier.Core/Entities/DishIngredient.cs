namespace Cuisinier.Core.Entities;

public class DishIngredient
{
    public int Id { get; set; }
    public int DishId { get; set; }
    public Dish? Dish { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
