namespace Cuisinier.Core.Entities;

public class ShoppingList
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public Menu? Menu { get; set; }
    public DateTime CreationDate { get; set; }
    
    public List<ShoppingListItem> Items { get; set; } = new();
    public List<ShoppingListDish> ShoppingListDishes { get; set; } = new();
}

