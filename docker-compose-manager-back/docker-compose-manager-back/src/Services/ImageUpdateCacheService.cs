using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for caching project update check results.
/// </summary>
public interface IImageUpdateCacheService
{
    /// <summary>
    /// Gets cached update check result for a project.
    /// </summary>
    ProjectUpdateCheckResponse? GetCachedCheck(string projectName);

    /// <summary>
    /// Sets cached update check result for a project.
    /// </summary>
    void SetCachedCheck(string projectName, ProjectUpdateCheckResponse result);

    /// <summary>
    /// Invalidates cache for a specific project.
    /// </summary>
    void InvalidateProject(string projectName);

    /// <summary>
    /// Invalidates all cached update checks.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Gets all cached project summaries.
    /// </summary>
    List<ProjectUpdateSummary> GetAllCachedSummaries();
}

public class ImageUpdateCacheService : IImageUpdateCacheService
{
    private readonly IMemoryCache _cache;
    private readonly UpdateCheckOptions _options;
    private readonly ILogger<ImageUpdateCacheService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Track cached project names for bulk operations
    private readonly HashSet<string> _cachedProjects = new();
    private readonly object _projectsLock = new();

    private const string CacheKeyPrefix = "image_update_";

    public ImageUpdateCacheService(
        IMemoryCache cache,
        IOptions<UpdateCheckOptions> options,
        ILogger<ImageUpdateCacheService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public ProjectUpdateCheckResponse? GetCachedCheck(string projectName)
    {
        string cacheKey = GetCacheKey(projectName);

        if (_cache.TryGetValue(cacheKey, out ProjectUpdateCheckResponse? cached))
        {
            _logger.LogDebug("Cache hit for project {ProjectName}", projectName);
            return cached;
        }

        _logger.LogDebug("Cache miss for project {ProjectName}", projectName);
        return null;
    }

    public void SetCachedCheck(string projectName, ProjectUpdateCheckResponse result)
    {
        string cacheKey = GetCacheKey(projectName);

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
                lock (_projectsLock)
                {
                    _cachedProjects.Remove(projectName);
                }
            }
        });

        _cache.Set(cacheKey, result, cacheOptions);

        lock (_projectsLock)
        {
            _cachedProjects.Add(projectName);
        }

        _logger.LogDebug("Cached update check for project {ProjectName}, expires in {Minutes} minutes",
            projectName, _options.CacheDurationMinutes);
    }

    public void InvalidateProject(string projectName)
    {
        string cacheKey = GetCacheKey(projectName);
        _cache.Remove(cacheKey);

        lock (_projectsLock)
        {
            _cachedProjects.Remove(projectName);
        }

        _logger.LogDebug("Invalidated cache for project {ProjectName}", projectName);
    }

    public void InvalidateAll()
    {
        List<string> projects;
        lock (_projectsLock)
        {
            projects = _cachedProjects.ToList();
            _cachedProjects.Clear();
        }

        foreach (string projectName in projects)
        {
            string cacheKey = GetCacheKey(projectName);
            _cache.Remove(cacheKey);
        }

        _logger.LogDebug("Invalidated all cached update checks ({Count} projects)", projects.Count);
    }

    public List<ProjectUpdateSummary> GetAllCachedSummaries()
    {
        var summaries = new List<ProjectUpdateSummary>();

        List<string> projects;
        lock (_projectsLock)
        {
            projects = _cachedProjects.ToList();
        }

        foreach (string projectName in projects)
        {
            ProjectUpdateCheckResponse? cached = GetCachedCheck(projectName);
            if (cached != null)
            {
                summaries.Add(new ProjectUpdateSummary(
                    ProjectName: cached.ProjectName,
                    ServicesWithUpdates: cached.Images.Count(i => i.UpdateAvailable),
                    LastChecked: cached.LastChecked
                ));
            }
        }

        return summaries;
    }

    private static string GetCacheKey(string projectName)
    {
        return $"{CacheKeyPrefix}{projectName.ToLowerInvariant()}";
    }
}
