using Cuisinier.Shared.DTOs;
using Refit;

namespace Cuisinier.App.Services;

public interface IFavoriteApi
{
    [Get("/api/favorite")]
    Task<List<FavoriteResponse>> GetAllFavoritesAsync();

    [Get("/api/favorite/{id}")]
    Task<FavoriteResponse> GetFavoriteAsync(int id);

    [Post("/api/favorite")]
    Task<FavoriteResponse> AddFavoriteAsync(FavoriteRequest request);

    [Put("/api/favorite/{id}")]
    Task<FavoriteResponse> UpdateFavoriteAsync(int id, FavoriteRequest request);

    [Delete("/api/favorite/{id}")]
    Task DeleteFavoriteAsync(int id);

    [Post("/api/favorite/check-duplicate")]
    Task<bool> CheckDuplicateAsync(CheckDuplicateRequest request);

    [Post("/api/favorite/check-duplicates-batch")]
    Task<CheckDuplicatesBatchResponse> CheckDuplicatesBatchAsync(CheckDuplicatesBatchRequest request);
}

public class CheckDuplicateRequest
{
    public string Title { get; set; } = string.Empty;
    public List<IngredientRequest> Ingredients { get; set; } = new();
}
