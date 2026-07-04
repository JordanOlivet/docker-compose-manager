using Lighthouse.Configuration;
using Lighthouse.Services;
using Microsoft.Extensions.Options;

namespace Lighthouse.BackgroundServices;

public class CleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupBackgroundService> _logger;
    private readonly PasswordResetOptions _options;

    public CleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<CleanupBackgroundService> logger,
        IOptions<PasswordResetOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token Cleanup Background Service starting. Cleanup interval: {Hours} hours",
            _options.CleanupIntervalHours);

        // Short settle delay so the first cleanup doesn't compete with startup work,
        // then run immediately (a fresh restart no longer waits a full interval).
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cleanup");
                // Continue running despite errors
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.CleanupIntervalHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Token Cleanup Background Service stopped");
    }

    private async Task RunCleanupAsync()
    {
        _logger.LogInformation("Running scheduled cleanup...");

        // Create a scope to resolve the scoped services
        using IServiceScope scope = _serviceProvider.CreateScope();

        IPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IPasswordResetService>();
        var deletedCount = await passwordResetService.CleanupExpiredTokensAsync();
        if (deletedCount > 0)
        {
            _logger.LogInformation("Token cleanup completed. Deleted {Count} expired tokens", deletedCount);
        }

        // Cleanup old operations (older than 7 days)
        IOperationService operationService = scope.ServiceProvider.GetRequiredService<IOperationService>();
        var operationDeletedCount = await operationService.CleanupOldOperationsAsync(
            DateTime.UtcNow.AddDays(-7));
        if (operationDeletedCount > 0)
        {
            _logger.LogInformation("Operation cleanup completed. Deleted {Count} old operations", operationDeletedCount);
        }

        // Cleanup expired / long-revoked refresh sessions
        AuthService authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        var sessionDeletedCount = await authService.CleanupExpiredSessionsAsync();
        if (sessionDeletedCount > 0)
        {
            _logger.LogInformation("Session cleanup completed. Deleted {Count} stale sessions", sessionDeletedCount);
        }
    }
}
