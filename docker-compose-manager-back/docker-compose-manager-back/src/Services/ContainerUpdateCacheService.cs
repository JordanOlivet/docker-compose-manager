using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for caching container update check results.
/// </summary>
public interface IContainerUpdateCacheService
{
    /// <summary>
    /// Gets cached update check result for a container.
    /// </summary>
    ContainerUpdateCheckResponse? GetCachedCheck(string containerId);

    /// <summary>
    /// Sets cached update check result for a container.
    /// </summary>
    void SetCachedCheck(string containerId, ContainerUpdateCheckResponse result);

    /// <summary>
    /// Invalidates cache for a specific container.
    /// </summary>
    void InvalidateContainer(string containerId);

    /// <summary>
    /// Invalidates all cached container update checks.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Gets all cached container update summaries.
    /// </summary>
    List<ContainerUpdateSummary> GetAllCachedSummaries();
}

public class ContainerUpdateCacheService : IContainerUpdateCacheService
{
    private readonly IMemoryCache _cache;
    private readonly UpdateCheckOptions _options;
    private readonly ILogger<ContainerUpdateCacheService> _logger;

    // Track cached container IDs for bulk operations
    private readonly HashSet<string> _cachedContainers = new();
    private readonly object _containersLock = new();

    private const string CacheKeyPrefix = "container_update_";

    public ContainerUpdateCacheService(
        IMemoryCache cache,
        IOptions<UpdateCheckOptions> options,
        ILogger<ContainerUpdateCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public ContainerUpdateCheckResponse? GetCachedCheck(string containerId)
    {
        string cacheKey = GetCacheKey(containerId);

        if (_cache.TryGetValue(cacheKey, out ContainerUpdateCheckResponse? cached))
        {
            _logger.LogDebug("Cache hit for container {ContainerId}", containerId);
            return cached;
        }

        _logger.LogDebug("Cache miss for container {ContainerId}", containerId);
        return null;
    }

    public void SetCachedCheck(string containerId, ContainerUpdateCheckResponse result)
    {
        string cacheKey = GetCacheKey(containerId);

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheDurationMinutes),
            SlidingExpiration = TimeSpan.FromMinutes(_options.CacheDurationMinutes / 2)
        };

        // Set up removal callback to clean up tracking
        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            if (reason != EvictionReason.Replaced)
            {
                lock (_containersLock)
                {
                    _cachedContainers.Remove(containerId);
                }
            }
        });

        _cache.Set(cacheKey, result, cacheOptions);

        lock (_containersLock)
        {
            _cachedContainers.Add(containerId);
        }

        _logger.LogDebug("Cached update check for container {ContainerId}, expires in {Minutes} minutes",
            containerId, _options.CacheDurationMinutes);
    }

    public void InvalidateContainer(string containerId)
    {
        string cacheKey = GetCacheKey(containerId);
        _cache.Remove(cacheKey);

        lock (_containersLock)
        {
            _cachedContainers.Remove(containerId);
        }

        _logger.LogDebug("Invalidated cache for container {ContainerId}", containerId);
    }

    public void InvalidateAll()
    {
        List<string> containers;
        lock (_containersLock)
        {
            containers = _cachedContainers.ToList();
            _cachedContainers.Clear();
        }

        foreach (string containerId in containers)
        {
            string cacheKey = GetCacheKey(containerId);
            _cache.Remove(cacheKey);
        }

        _logger.LogDebug("Invalidated all cached container update checks ({Count} containers)", containers.Count);
    }

    public List<ContainerUpdateSummary> GetAllCachedSummaries()
    {
        var summaries = new List<ContainerUpdateSummary>();

        List<string> containers;
        lock (_containersLock)
        {
            containers = _cachedContainers.ToList();
        }

        foreach (string containerId in containers)
        {
            ContainerUpdateCheckResponse? cached = GetCachedCheck(containerId);
            if (cached != null)
            {
                summaries.Add(new ContainerUpdateSummary(
                    ContainerId: cached.ContainerId,
                    ContainerName: cached.ContainerName,
                    Image: cached.Image,
                    UpdateAvailable: cached.UpdateAvailable,
                    IsComposeManaged: cached.IsComposeManaged,
                    ProjectName: cached.ProjectName
                ));
            }
        }

        return summaries;
    }

    private static string GetCacheKey(string containerId)
    {
        return $"{CacheKeyPrefix}{containerId.ToLowerInvariant()}";
    }
}
