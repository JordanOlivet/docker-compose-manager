using docker_compose_manager_back.Services;

namespace docker_compose_manager_back.BackgroundServices;

public class ComposeFileDiscoveryService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ComposeFileDiscoveryService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1); // Scan every  minutes

    public ComposeFileDiscoveryService(
        IServiceProvider serviceProvider,
        ILogger<ComposeFileDiscoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Compose File Discovery Service started");

        _logger.LogInformation("Initial discovery scan started ...");

        // Run initial scan
        await ScanFilesAsync(stoppingToken);

        _logger.LogInformation("Initial discovery scan done");
        _logger.LogInformation($"Recurring discovery scan starting. Scanning every {_interval.Minutes} minutes ...");

        // Then run periodic scans
        using PeriodicTimer timer = new(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ScanFilesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Compose File Discovery Service is stopping");
        }
    }

    private async Task ScanFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting compose file discovery scan");

            using IServiceScope scope = _serviceProvider.CreateScope();
            FileService fileService = scope.ServiceProvider.GetRequiredService<FileService>();

            int syncedCount = await fileService.SyncDatabaseWithDiscoveredFilesAsync();

            _logger.LogInformation(
                "Compose file discovery scan completed. Synced {Count} files",
                syncedCount
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compose file discovery scan");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Compose File Discovery Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
