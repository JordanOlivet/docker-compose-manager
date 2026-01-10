namespace docker_compose_manager_back.Services;

using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

/// <summary>
/// Thread-safe caching implementation for discovered compose files.
/// Uses double-check locking pattern with SemaphoreSlim to prevent concurrent filesystem scans.
/// </summary>
/// <remarks>
/// <para>
/// This service wraps the IComposeFileScanner with an in-memory cache to avoid
/// repeated filesystem scans. The cache is populated on-demand and expires based
/// on the configured TTL (ComposeDiscoveryOptions.CacheDurationSeconds).
/// </para>
/// <para>
/// Thread-Safety Implementation:
/// - SemaphoreSlim (1,1) ensures only one thread can perform a scan at a time
/// - Double-check locking: First check cache without lock (fast path for hits),
///   then acquire lock, then check again to avoid race conditions
/// - All waiting threads receive the same cached result after the first scan completes
/// </para>
/// <para>
/// Performance Characteristics:
/// - Cache HIT: O(1) lookup, no lock contention
/// - Cache MISS: One thread scans while others wait, all receive same result
/// - Bypass cache: Clears cache and performs fresh scan under lock
/// </para>
/// </remarks>
public class ComposeFileCacheService : IComposeFileCacheService
{
    private const string CacheKey = "compose_file_discovery";

    private readonly IMemoryCache _cache;
    private readonly IComposeFileScanner _scanner;
    private readonly IOptions<ComposeDiscoveryOptions> _options;
    private readonly ILogger<ComposeFileCacheService> _logger;
    private readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Initializes a new instance of the ComposeFileCacheService.
    /// </summary>
    /// <param name="cache">Memory cache for storing discovered compose files.</param>
    /// <param name="scanner">Scanner service for discovering compose files from filesystem.</param>
    /// <param name="options">Configuration options for compose discovery (cache TTL, paths, etc.).</param>
    /// <param name="logger">Logger for diagnostic and monitoring information.</param>
    public ComposeFileCacheService(
        IMemoryCache cache,
        IComposeFileScanner scanner,
        IOptions<ComposeDiscoveryOptions> options,
        ILogger<ComposeFileCacheService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets discovered compose files from cache, or scans filesystem if cache is empty/expired.
    /// Implements double-check locking pattern for thread-safe cache population.
    /// </summary>
    /// <param name="bypassCache">If true, forces a fresh scan by clearing cache first.</param>
    /// <returns>List of discovered compose files with metadata.</returns>
    /// <remarks>
    /// <para>
    /// Execution flow:
    /// 1. If bypassCache=true, remove cache entry
    /// 2. First check: Try to get from cache without lock (fast path)
    /// 3. If cache miss: Acquire semaphore lock to prevent concurrent scans
    /// 4. Second check: Re-check cache after lock (another thread may have filled it)
    /// 5. If still empty: Perform scan and populate cache with configured TTL
    /// 6. Release lock and return result
    /// </para>
    /// <para>
    /// This approach ensures:
    /// - Cache hits are fast (no locking overhead)
    /// - Only one scan happens at a time (no duplicate filesystem operations)
    /// - All waiting threads get the same cached result
    /// </para>
    /// </remarks>
    public async Task<List<DiscoveredComposeFile>> GetOrScanAsync(bool bypassCache = false)
    {
        if (bypassCache)
        {
            _cache.Remove(CacheKey);
            _logger.LogDebug("Cache bypassed, forcing refresh");
        }

        // First check without lock (fast path for cache hits)
        if (_cache.TryGetValue(CacheKey, out List<DiscoveredComposeFile>? cached))
        {
            _logger.LogDebug("Cache HIT for compose file discovery");
            return cached;
        }

        _logger.LogDebug("Cache MISS for compose file discovery - acquiring lock");

        // Prevent concurrent scans - only one thread scans at a time
        await _scanLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            // (another thread may have populated cache while we were waiting for lock)
            if (_cache.TryGetValue(CacheKey, out cached))
            {
                _logger.LogDebug("Cache HIT after lock acquisition (another thread filled cache)");
                return cached;
            }

            // Perform filesystem scan
            _logger.LogInformation("Starting compose file discovery scan");
            var discovered = await _scanner.ScanComposeFilesAsync();

            // Cache with configured TTL
            var ttl = TimeSpan.FromSeconds(_options.Value.CacheDurationSeconds);
            _cache.Set(CacheKey, discovered, ttl);

            _logger.LogInformation(
                "Cache populated with {Count} compose files, TTL: {TtlSeconds}s",
                discovered.Count,
                _options.Value.CacheDurationSeconds);

            return discovered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compose file discovery scan");
            throw;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    /// <summary>
    /// Invalidates the compose file discovery cache.
    /// </summary>
    /// <remarks>
    /// This does NOT acquire a lock. It simply removes the cache entry.
    /// The next call to GetOrScanAsync will trigger a fresh scan under lock.
    ///
    /// Call this method when:
    /// - Compose files are added/modified/deleted externally
    /// - Configuration changes affect discovery (paths, patterns, etc.)
    /// - Manual refresh is needed (e.g., via admin UI)
    /// </remarks>
    public void Invalidate()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("Compose file discovery cache invalidated");
    }
}
