using Cuisinier.Core.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IDishApi
{
    [Get("/api/dish")]
    Task<DishListResponse> GetAllDishesAsync([Query] DishFilterRequest filter);

    [Get("/api/dish/{id}")]
    Task<DishResponse> GetDishAsync(int id);

    [Post("/api/dish")]
    Task<DishResponse> AddDishAsync(DishRequest request);

    [Put("/api/dish/{id}")]
    Task<DishResponse> UpdateDishAsync(int id, DishRequest request);

    [Delete("/api/dish/{id}")]
    Task DeleteDishAsync(int id);

    [Post("/api/dish/check-duplicate")]
    Task<bool> CheckDuplicateAsync(CheckDuplicateRequest request);
}
