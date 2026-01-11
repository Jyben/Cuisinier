using Microsoft.AspNetCore.Identity;

namespace Cuisinier.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public List<Dish> Dishes { get; set; } = new();
    public List<Menu> Menus { get; set; } = new();
    public List<Favorite> Favorites { get; set; } = new();
    public List<ShoppingList> ShoppingLists { get; set; } = new();
    public List<RefreshToken> RefreshTokens { get; set; } = new();
    public MenuSettings? MenuSettings { get; set; }
}