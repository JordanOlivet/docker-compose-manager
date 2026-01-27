namespace docker_compose_manager_back.Services;

using docker_compose_manager_back.Models;

/// <summary>
/// Thread-safe caching service for discovered compose files.
/// Provides cached access to compose file discovery results to avoid repeated filesystem scans.
/// </summary>
/// <remarks>
/// <para>
/// This service implements a thread-safe caching mechanism using double-check locking pattern
/// to prevent concurrent filesystem scans while maintaining high performance for cache hits.
/// </para>
/// <para>
/// Thread-Safety Approach:
/// 1. Fast path: Check cache without lock (optimized for cache hits)
/// 2. Lock acquisition: Use SemaphoreSlim to ensure only one scan happens at a time
/// 3. Double-check: Re-check cache after lock to avoid redundant scans
/// 4. Scan and cache: Perform scan only if cache still empty after lock
/// </para>
/// <para>
/// Cache Behavior:
/// - Cache key: "compose_file_discovery"
/// - TTL: Configured via ComposeDiscoveryOptions.CacheDurationSeconds
/// - Invalidation: Manual via Invalidate() or automatic after TTL expires
/// - Bypass: Use bypassCache parameter to force refresh
/// </para>
/// </remarks>
public interface IComposeFileCacheService
{
    /// <summary>
    /// Gets discovered compose files from cache, or scans filesystem if cache is empty/expired.
    /// </summary>
    /// <param name="bypassCache">If true, forces a fresh scan and cache update.</param>
    /// <returns>List of discovered compose files with metadata.</returns>
    /// <remarks>
    /// This method is thread-safe. Multiple concurrent calls will result in only one filesystem scan,
    /// with other threads waiting for the scan to complete and then receiving the cached result.
    /// </remarks>
    Task<List<DiscoveredComposeFile>> GetOrScanAsync(bool bypassCache = false);

    /// <summary>
    /// Invalidates the compose file discovery cache, forcing the next GetOrScanAsync call to perform a fresh scan.
    /// </summary>
    /// <remarks>
    /// This method should be called when:
    /// - Compose files are added, modified, or deleted outside of normal application flow
    /// - Configuration changes that affect compose file discovery
    /// - Manual cache refresh is needed
    /// </remarks>
    void Invalidate();
}
