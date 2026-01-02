namespace Cuisinier.Core.DTOs;

public class AddItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

