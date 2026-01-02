namespace Cuisinier.Core.DTOs;

public class ShoppingListItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsManuallyAdded { get; set; }
}

