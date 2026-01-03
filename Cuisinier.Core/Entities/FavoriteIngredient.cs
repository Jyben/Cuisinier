namespace Cuisinier.Core.Entities;

public class FavoriteIngredient
{
    public int Id { get; set; }
    public int FavoriteId { get; set; }
    public Favorite? Favorite { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
