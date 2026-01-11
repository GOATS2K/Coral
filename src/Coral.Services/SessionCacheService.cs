using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Coral.Services;

public record CachedSession(
    Guid DeviceId,
    Guid TokenId,
    DateTime SessionExpiresAt,
    DateTime LastSeenAt,
    DateTime CachedAt
);

public interface ISessionCacheService
{
    CachedSession? GetSession(Guid deviceId);
    void SetSession(Guid deviceId, Guid tokenId, DateTime sessionExpiresAt, DateTime lastSeenAt);
    void InvalidateSession(Guid deviceId);
    bool ShouldUpdateLastSeen(CachedSession session);
}

public class SessionCacheService : ISessionCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SessionCacheService> _logger;

    // Cache entries for 2 minutes - short enough to catch invalidations reasonably quickly
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    // Only update LastSeenAt in the database if the cached value is older than 5 minutes
    private static readonly TimeSpan LastSeenUpdateThreshold = TimeSpan.FromMinutes(5);

    public SessionCacheService(IMemoryCache cache, ILogger<SessionCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private static string GetCacheKey(Guid deviceId) => $"session:{deviceId}";

    public CachedSession? GetSession(Guid deviceId)
    {
        var cacheKey = GetCacheKey(deviceId);
        if (_cache.TryGetValue(cacheKey, out CachedSession? session))
        {
            _logger.LogDebug("Session cache hit for device {DeviceId}", deviceId);
            return session;
        }

        _logger.LogDebug("Session cache miss for device {DeviceId}", deviceId);
        return null;
    }

    public void SetSession(Guid deviceId, Guid tokenId, DateTime sessionExpiresAt, DateTime lastSeenAt)
    {
        var cacheKey = GetCacheKey(deviceId);
        var session = new CachedSession(deviceId, tokenId, sessionExpiresAt, lastSeenAt, DateTime.UtcNow);

        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(CacheDuration)
            .SetSize(1); // Each session entry counts as 1 unit towards the cache size limit

        _cache.Set(cacheKey, session, options);
        _logger.LogDebug("Session cached for device {DeviceId}, expires at {ExpiresAt}", deviceId, sessionExpiresAt);
    }

    public void InvalidateSession(Guid deviceId)
    {
        var cacheKey = GetCacheKey(deviceId);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Session cache invalidated for device {DeviceId}", deviceId);
    }

    public bool ShouldUpdateLastSeen(CachedSession session)
    {
        // Only update LastSeenAt if it's been more than the threshold since the last update
        return DateTime.UtcNow - session.LastSeenAt > LastSeenUpdateThreshold;
    }
}
