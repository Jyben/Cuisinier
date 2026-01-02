using Cuisinier.Core.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IShoppingListApi
{
    [Get("/api/shoppinglist/{menuId}")]
    Task<ShoppingListResponse> GetShoppingListAsync(int menuId);
    
    [Post("/api/shoppinglist/{menuId}/item")]
    Task<ShoppingListItemResponse> AddItemAsync(int menuId, [Body] AddItemRequest request);
    
    [Delete("/api/shoppinglist/{menuId}/item/{itemId}")]
    Task DeleteItemAsync(int menuId, int itemId);
    
    [Delete("/api/shoppinglist/{menuId}")]
    Task DeleteShoppingListAsync(int menuId);
    
    [Post("/api/shoppinglist/{menuId}/validate")]
    Task<ShoppingListResponse> ValidateShoppingListAsync(int menuId);
    
    [Post("/api/shoppinglist/{menuId}/generate-detailed-recipes")]
    Task<MenuResponse> GenerateDetailedRecipesAsync(int menuId);
}

