namespace Cuisinier.Core.DTOs;

public class ValidateMenuRequest
{
    public List<int>? FavoriteIds { get; set; }
    public List<int>? DishIds { get; set; }
}
