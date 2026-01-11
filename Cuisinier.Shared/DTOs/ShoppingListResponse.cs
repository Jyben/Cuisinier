namespace Cuisinier.Shared.DTOs;

public class ShoppingListResponse
{
    public int Id { get; set; }
    public int MenuId { get; set; }
    public DateTime CreationDate { get; set; }
    public List<ShoppingListItemResponse> Items { get; set; } = new();
}

