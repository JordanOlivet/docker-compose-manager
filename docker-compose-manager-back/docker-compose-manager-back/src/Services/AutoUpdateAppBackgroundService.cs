using Cronos;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
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

    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxSleep = TimeSpan.FromHours(1);

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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                (bool enabled, string cron) = await ReadSettingsAsync(stoppingToken);

                if (!enabled)
                {
                    await Task.Delay(IdlePollInterval, stoppingToken);
                    continue;
                }

                if (!TryParseCron(cron, out CronExpression? expression))
                {
                    _logger.LogWarning("Invalid cron expression '{Cron}' for AutoUpdateApp. Sleeping {Seconds}s.",
                        cron, IdlePollInterval.TotalSeconds);
                    await Task.Delay(IdlePollInterval, stoppingToken);
                    continue;
                }

                DateTime nowUtc = DateTime.UtcNow;
                DateTime? next = expression!.GetNextOccurrence(nowUtc);
                if (next == null)
                {
                    await Task.Delay(IdlePollInterval, stoppingToken);
                    continue;
                }

                TimeSpan wait = next.Value - nowUtc;
                if (wait > MaxSleep) wait = MaxSleep;
                if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;

                _logger.LogDebug("Next AutoUpdateApp check at {Next:O} (sleeping {Wait})", next.Value, wait);
                await Task.Delay(wait, stoppingToken);

                if (DateTime.UtcNow < next.Value)
                {
                    continue;
                }

                await RunCheckAndUpdateAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoUpdateAppBackgroundService loop");
                await Task.Delay(IdlePollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("AutoUpdateAppBackgroundService stopped");
    }

    private async Task RunCheckAndUpdateAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        ISelfUpdateService selfUpdate = scope.ServiceProvider.GetRequiredService<ISelfUpdateService>();
        IAuditService audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

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

            if (!trigger.Success)
            {
                _logger.LogWarning("AutoUpdateApp: trigger returned failure: {Message}", trigger.Message);
                // Roll back the pending flag if the update did not actually start
                await SetPendingComposeAutoUpdateFlagAsync(scope, false, ct);
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
