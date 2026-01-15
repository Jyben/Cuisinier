namespace Cuisinier.Api.Services;

public interface IUserAccessService
{
    Task<List<string>> GetAccessibleUserIdsAsync(string userId);
    void InvalidateCache(string userId);
    void InvalidateCacheForBothUsers(string user1Id, string user2Id);
}
