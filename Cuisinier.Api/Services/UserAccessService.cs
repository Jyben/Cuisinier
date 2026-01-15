using Cuisinier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cuisinier.Api.Services;

public class UserAccessService : IUserAccessService
{
    private readonly CuisinierDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UserAccessService> _logger;
    private const string AccessibleUserIdsCacheKeyPrefix = "FamilyLink_AccessibleUserIds_";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public UserAccessService(
        CuisinierDbContext context,
        IMemoryCache cache,
        ILogger<UserAccessService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<string>> GetAccessibleUserIdsAsync(string userId)
    {
        var cacheKey = $"{AccessibleUserIdsCacheKeyPrefix}{userId}";

        if (_cache.TryGetValue(cacheKey, out List<string>? cachedIds) && cachedIds != null)
        {
            _logger.LogDebug("Retrieved accessible user IDs from cache for user {UserId}", userId);
            return cachedIds;
        }

        var link = await _context.FamilyLinks
            .FirstOrDefaultAsync(fl => fl.User1Id == userId || fl.User2Id == userId);

        List<string> userIds;
        if (link == null)
        {
            userIds = new List<string> { userId };
        }
        else
        {
            var linkedUserId = link.User1Id == userId ? link.User2Id : link.User1Id;
            userIds = new List<string> { userId, linkedUserId };
        }

        _cache.Set(cacheKey, userIds, CacheExpiration);
        _logger.LogDebug("Cached accessible user IDs for user {UserId}: {UserIds}", userId, string.Join(", ", userIds));

        return userIds;
    }

    public void InvalidateCache(string userId)
    {
        var cacheKey = $"{AccessibleUserIdsCacheKeyPrefix}{userId}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated accessible user IDs cache for user {UserId}", userId);
    }

    public void InvalidateCacheForBothUsers(string user1Id, string user2Id)
    {
        InvalidateCache(user1Id);
        InvalidateCache(user2Id);

        // Also invalidate menus cache for both users
        _cache.Remove($"Menu_All_{user1Id}");
        _cache.Remove($"Menu_All_{user2Id}");

        _logger.LogInformation("Invalidated all family-related caches for users {User1Id} and {User2Id}", user1Id, user2Id);
    }
}
