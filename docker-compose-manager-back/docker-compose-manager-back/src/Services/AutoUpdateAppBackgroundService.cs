using Cronos;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using docker_compose_manager_back.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Background service that triggers the application self-update on a cron schedule.
/// Disabled by default. Before triggering, marks PendingComposeAutoUpdate so the
/// compose auto-update cycle is replayed after the application restarts.
/// </summary>
public class AutoUpdateAppBackgroundService : BackgroundService
{
    public const string EnabledKey = "AutoUpdateAppEnabled";
    public const string CronKey = "AutoUpdateAppCron";
    public const string DefaultCron = "0 2 * * *";

    // Short fixed tick: every tick we check whether a cron occurrence fell within the
    // elapsed window. This never misses the scheduled minute and picks up enable/cron
    // changes within one tick (unlike a blind Task.Delay until the next occurrence).
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoUpdateAppBackgroundService> _logger;

    public AutoUpdateAppBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoUpdateAppBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoUpdateAppBackgroundService starting");

        // Window anchor: we fire when a cron occurrence falls in (lastCheckUtc, nowUtc].
        DateTime lastCheckUtc = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TickInterval, stoppingToken);

                (bool enabled, string cron) = await ReadSettingsAsync(stoppingToken);
                DateTime nowUtc = DateTime.UtcNow;

                if (!enabled)
                {
                    // Advance the window so occurrences during the disabled period don't fire later
                    lastCheckUtc = nowUtc;
                    continue;
                }

                if (!TryParseCron(cron, out CronExpression? expression))
                {
                    _logger.LogWarning("Invalid cron expression '{Cron}' for AutoUpdateApp.", cron);
                    lastCheckUtc = nowUtc;
                    continue;
                }

                DateTime? occurrence = expression!.GetNextOccurrence(lastCheckUtc, inclusive: false);
                if (occurrence.HasValue && occurrence.Value <= nowUtc)
                {
                    _logger.LogInformation("AutoUpdateApp: scheduled occurrence {Occurrence:O} is due, running check/update", occurrence.Value);
                    await RunCheckAndUpdateAsync(stoppingToken);
                }

                lastCheckUtc = nowUtc;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoUpdateAppBackgroundService loop");
            }
        }

        _logger.LogInformation("AutoUpdateAppBackgroundService stopped");
    }

    private async Task RunCheckAndUpdateAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        ISelfUpdateService selfUpdate = scope.ServiceProvider.GetRequiredService<ISelfUpdateService>();
        IAuditService audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        INotificationService notify = scope.ServiceProvider.GetRequiredService<INotificationService>();

        try
        {
            DTOs.AppUpdateCheckResponse check = await selfUpdate.CheckUpdateAsync(ct);
            if (!check.UpdateAvailable)
            {
                _logger.LogInformation("AutoUpdateApp: no update available (current: {Current}, latest: {Latest})",
                    check.CurrentVersion, check.LatestVersion);
                return;
            }

            _logger.LogInformation("AutoUpdateApp: update available {Current} -> {Latest}, triggering self-update",
                check.CurrentVersion, check.LatestVersion);

            // Set pending flag so compose auto-update replays after restart
            await SetPendingComposeAutoUpdateFlagAsync(scope, true, ct);
            // Record the target version so the new instance can confirm completion
            await SetPendingAppUpdateNotificationAsync(scope, check.LatestVersion, ct);

            DTOs.UpdateTriggerResponse trigger = await selfUpdate.TriggerUpdateAsync(
                userId: 1,
                ipAddress: "system",
                cancellationToken: ct);

            await audit.LogActionAsync(
                userId: null,
                action: AuditActions.AutoUpdateAppTriggered,
                ipAddress: "system",
                details: trigger.Success
                    ? $"Triggered update {check.CurrentVersion} -> {check.LatestVersion} (op: {trigger.OperationId})"
                    : $"Trigger failed: {trigger.Message}",
                resourceType: "auto_update",
                resourceId: "app");

            if (trigger.Success)
            {
                await notify.NotifyAppUpdateStartedAsync(check.CurrentVersion, check.LatestVersion, ct);
            }
            else
            {
                _logger.LogWarning("AutoUpdateApp: trigger returned failure: {Message}", trigger.Message);
                // Roll back the pending flags if the update did not actually start
                await SetPendingComposeAutoUpdateFlagAsync(scope, false, ct);
                await SetPendingAppUpdateNotificationAsync(scope, null, ct);
                await notify.NotifyAppUpdateFailedAsync(check.CurrentVersion, check.LatestVersion, trigger.Message, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AutoUpdateApp check/update cycle");
        }
    }

    private async Task<(bool Enabled, string Cron)> ReadSettingsAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            AppSetting? enabledSetting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == EnabledKey, ct);
            AppSetting? cronSetting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == CronKey, ct);

            bool enabled = enabledSetting != null
                && bool.TryParse(enabledSetting.Value, out bool b)
                && b;

            string cron = !string.IsNullOrWhiteSpace(cronSetting?.Value) ? cronSetting!.Value : DefaultCron;
            return (enabled, cron);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read AutoUpdateApp settings; treating as disabled");
            return (false, DefaultCron);
        }
    }

    private static async Task SetPendingComposeAutoUpdateFlagAsync(IServiceScope scope, bool value, CancellationToken ct)
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(
            s => s.Key == AutoUpdateComposeBackgroundService.PendingKey, ct);
        if (setting == null)
        {
            setting = new AppSetting
            {
                Key = AutoUpdateComposeBackgroundService.PendingKey,
                Value = value ? "true" : "false"
            };
            db.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = value ? "true" : "false";
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Upserts (or clears, when <paramref name="targetVersion"/> is null) the
    /// pending app-update notification flag so the new instance can confirm the
    /// update completed after restart.
    /// </summary>
    private static async Task SetPendingAppUpdateNotificationAsync(IServiceScope scope, string? targetVersion, CancellationToken ct)
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(
            s => s.Key == AppUpdateNotificationStartupService.PendingKey, ct);

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            if (setting != null)
            {
                db.AppSettings.Remove(setting);
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        if (setting == null)
        {
            setting = new AppSetting { Key = AppUpdateNotificationStartupService.PendingKey, Value = targetVersion };
            db.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = targetVersion;
        }
        await db.SaveChangesAsync(ct);
    }

    private static bool TryParseCron(string expression, out CronExpression? parsed)
    {
        try
        {
            CronFormat format = expression.Trim().Split(' ').Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;
            parsed = CronExpression.Parse(expression, format);
            return true;
        }
        catch
        {
            parsed = null;
            return false;
        }
    }
}
