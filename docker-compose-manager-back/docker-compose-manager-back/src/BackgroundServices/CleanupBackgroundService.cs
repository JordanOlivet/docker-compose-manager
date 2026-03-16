using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Services;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.BackgroundServices;

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.CleanupIntervalHours), stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                _logger.LogInformation("Running password reset token cleanup...");

                // Create a scope to resolve the scoped service
                using IServiceScope scope = _serviceProvider.CreateScope();
                IPasswordResetService passwordResetService = scope.ServiceProvider.GetRequiredService<IPasswordResetService>();

                var deletedCount = await passwordResetService.CleanupExpiredTokensAsync();

                if (deletedCount > 0)
                {
                    _logger.LogInformation("Token cleanup completed. Deleted {Count} expired tokens", deletedCount);
                }
                else
                {
                    _logger.LogDebug("Token cleanup completed. No expired tokens to delete");
                }

                // Cleanup old operations (older than 7 days)
                OperationService operationService = scope.ServiceProvider.GetRequiredService<OperationService>();
                var operationDeletedCount = await operationService.CleanupOldOperationsAsync(
                    DateTime.UtcNow.AddDays(-7));
                if (operationDeletedCount > 0)
                {
                    _logger.LogInformation("Operation cleanup completed. Deleted {Count} old operations", operationDeletedCount);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("Token Cleanup Background Service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during token cleanup");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("Token Cleanup Background Service stopped");
    }
}
