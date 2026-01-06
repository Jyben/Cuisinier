namespace Cuisinier.Core.Entities;

public class ShoppingListDish
{
    public int ShoppingListId { get; set; }
    public ShoppingList? ShoppingList { get; set; }
    public int DishId { get; set; }
    public Dish? Dish { get; set; }
}
