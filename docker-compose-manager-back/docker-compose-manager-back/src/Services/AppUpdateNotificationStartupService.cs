using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Services;

/// <summary>
/// On application startup, sends an "update completed" notification if a self
/// auto-update was triggered before the last restart. The trigger flow stores the
/// target version in the <see cref="PendingKey"/> AppSetting; here we confirm the
/// running version matches it, notify, and clear the flag. If the version did not
/// change (update did not land), the flag is cleared without notifying to avoid a
/// false confirmation on an unrelated restart.
/// </summary>
public class AppUpdateNotificationStartupService : BackgroundService
{
    /// <summary>AppSetting key holding the target version of a pending self-update notification.</summary>
    public const string PendingKey = "PendingAppUpdateNotification";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppUpdateNotificationStartupService> _logger;

    public AppUpdateNotificationStartupService(
        IServiceProvider serviceProvider,
        ILogger<AppUpdateNotificationStartupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            AppSetting? pending = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == PendingKey, stoppingToken);
            if (pending == null || string.IsNullOrWhiteSpace(pending.Value))
            {
                return;
            }

            string targetVersion = pending.Value;

            IVersionDetectionService versionService = scope.ServiceProvider.GetRequiredService<IVersionDetectionService>();
            string currentVersion = await versionService.GetCurrentVersionAsync();

            if (VersionsMatch(currentVersion, targetVersion))
            {
                INotificationService notify = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notify.NotifyAppUpdateCompletedAsync(currentVersion, stoppingToken);
                _logger.LogInformation("Sent app update completed notification for version {Version}", currentVersion);
            }
            else
            {
                _logger.LogInformation(
                    "Pending app update notification target {Target} does not match current version {Current}; clearing without notifying",
                    targetVersion, currentVersion);
            }

            db.AppSettings.Remove(pending);
            await db.SaveChangesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process pending app update notification");
        }
    }

    private static bool VersionsMatch(string current, string target)
    {
        return string.Equals(
            current.TrimStart('v', 'V'),
            target.TrimStart('v', 'V'),
            StringComparison.OrdinalIgnoreCase);
    }
}
