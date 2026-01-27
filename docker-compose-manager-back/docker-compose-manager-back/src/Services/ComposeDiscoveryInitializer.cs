using Microsoft.Extensions.Hosting;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Background service that performs initial compose files discovery on application startup.
/// Runs asynchronously without blocking the application startup process.
/// </summary>
/// <remarks>
/// <para>
/// This hosted service triggers an initial filesystem scan when the application starts,
/// ensuring that the compose file cache is populated early. The scan runs in the background
/// using Task.Run to avoid blocking the application startup.
/// </para>
/// <para>
/// Non-Blocking Startup Design:
/// - StartAsync returns immediately without waiting for the scan to complete
/// - Scan executes in background task (Task.Run with fire-and-forget pattern)
/// - Application can start serving requests while scan is in progress
/// - Errors during scan are logged but do not prevent application startup
/// </para>
/// <para>
/// IServiceProvider Usage:
/// - Constructor receives IServiceProvider instead of scoped services directly
/// - Creates a scope in the background task to resolve IComposeFileCacheService
/// - Required because IHostedService is a singleton but IComposeFileCacheService is scoped
/// - Scope is properly disposed after scan completes
/// </para>
/// <para>
/// Error Handling:
/// - Scan errors are caught and logged as errors (LogError)
/// - Application continues to start even if scan fails
/// - Failed scans will be retried on next cache access or scheduled refresh
/// - This ensures resilience: app doesn't crash if initial scan encounters issues
/// </para>
/// <para>
/// Cache Behavior:
/// - Calls GetOrScanAsync(bypassCache: true) to force fresh scan
/// - This populates the cache for the first time
/// - Subsequent API calls can use cached results (no filesystem scan needed)
/// - Cache TTL is controlled by ComposeDiscoveryOptions.CacheDurationSeconds
/// </para>
/// </remarks>
public class ComposeDiscoveryInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComposeDiscoveryInitializer> _logger;

    /// <summary>
    /// Initializes a new instance of the ComposeDiscoveryInitializer.
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scopes to resolve scoped services.</param>
    /// <param name="logger">Logger for diagnostic and monitoring information.</param>
    /// <remarks>
    /// IServiceProvider is used instead of direct dependency injection because:
    /// - IHostedService is registered as a singleton
    /// - IComposeFileCacheService is scoped (requires DbContext which is scoped)
    /// - Cannot inject scoped service into singleton directly
    /// - Solution: Inject IServiceProvider and create scope when needed
    /// </remarks>
    public ComposeDiscoveryInitializer(
        IServiceProvider serviceProvider,
        ILogger<ComposeDiscoveryInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// Initiates a background scan without blocking application startup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>A completed task (non-blocking).</returns>
    /// <remarks>
    /// <para>
    /// This method returns immediately (Task.CompletedTask) to allow the application
    /// to finish startup quickly. The actual scan runs in a background Task.Run.
    /// </para>
    /// <para>
    /// Fire-and-Forget Pattern:
    /// - Uses discard operator (_) to indicate intentional fire-and-forget
    /// - Background task continues running after StartAsync returns
    /// - Errors in background task are caught and logged internally
    /// </para>
    /// <para>
    /// Execution Flow:
    /// 1. StartAsync called by ASP.NET Core host during startup
    /// 2. Task.Run scheduled to run scan in background thread pool
    /// 3. StartAsync returns immediately (non-blocking)
    /// 4. Application continues startup, can serve requests
    /// 5. Background scan completes asynchronously
    /// 6. Cache is populated for future API calls
    /// </para>
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Run scan in background - don't block startup
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting initial compose files scan...");

                // Create scope to resolve scoped services
                // (IComposeFileCacheService is scoped, IHostedService is singleton)
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<IComposeFileCacheService>();

                // Perform initial scan with cache bypass to force fresh discovery
                var files = await cacheService.GetOrScanAsync(bypassCache: true);

                _logger.LogInformation(
                    "Initial compose files scan completed. Found {Count} files.",
                    files.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initial compose files scan");
                // Don't throw - app should start even if scan fails
                // Next cache access or scheduled refresh will retry the scan
            }
        }, cancellationToken);

        return Task.CompletedTask; // Non-blocking return
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown timeout.</param>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// This initializer doesn't maintain long-running background tasks, so there's
    /// nothing to clean up on shutdown. The background scan task will be cancelled
    /// automatically via the cancellationToken if still running.
    /// </remarks>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ComposeDiscoveryInitializer stopping");
        return Task.CompletedTask;
    }
}
