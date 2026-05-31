using Cronos;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.Models;
using Microsoft.EntityFrameworkCore;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Background service that runs compose project auto-updates on a cron schedule.
/// Disabled by default. Skips projects flagged with x-auto-update: false.
/// Defers cycles when a self-update is in progress and re-runs immediately
/// on the next application start via the PendingComposeAutoUpdate flag.
/// </summary>
public class AutoUpdateComposeBackgroundService : BackgroundService
{
    public const string EnabledKey = "AutoUpdateComposeEnabled";
    public const string CronKey = "AutoUpdateComposeCron";
    public const string PendingKey = "PendingComposeAutoUpdate";
    public const string DefaultCron = "0 2 * * *";

    // Short fixed tick: every tick we check whether a cron occurrence fell within the
    // elapsed window. This never misses the scheduled minute and picks up enable/cron
    // changes within one tick (unlike a blind Task.Delay until the next occurrence).
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoUpdateComposeBackgroundService> _logger;
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    public AutoUpdateComposeBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoUpdateComposeBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoUpdateComposeBackgroundService starting");

        // On startup, consume the pending flag (previous app self-update preempted a compose cycle)
        await ConsumePendingAutoUpdateFlagAsync(stoppingToken);

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
                    _logger.LogWarning("Invalid cron expression '{Cron}' for AutoUpdateCompose.", cron);
                    lastCheckUtc = nowUtc;
                    continue;
                }

                DateTime? occurrence = expression!.GetNextOccurrence(lastCheckUtc, inclusive: false);
                if (occurrence.HasValue && occurrence.Value <= nowUtc)
                {
                    _logger.LogInformation("AutoUpdateCompose: scheduled occurrence {Occurrence:O} is due, running cycle", occurrence.Value);
                    await RunCycleAsync("scheduled", stoppingToken);
                }

                lastCheckUtc = nowUtc;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoUpdateComposeBackgroundService loop");
            }
        }

        _logger.LogInformation("AutoUpdateComposeBackgroundService stopped");
    }

    private async Task RunCycleAsync(string trigger, CancellationToken ct)
    {
        if (!await _cycleLock.WaitAsync(0, ct))
        {
            _logger.LogDebug("Auto-update compose cycle already in progress, skipping");
            return;
        }

        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();

            ISelfUpdateService selfUpdateService = scope.ServiceProvider.GetRequiredService<ISelfUpdateService>();
            if (selfUpdateService.IsUpdateInProgress)
            {
                _logger.LogInformation("Self-update in progress, deferring compose auto-update cycle");
                await SetPendingAutoUpdateFlagAsync(scope, true, ct);
                await LogAuditSafelyAsync(scope, AuditActions.AutoUpdateComposeSkipped, "Skipped because self-update is in progress");
                return;
            }

            IComposeUpdateService composeUpdateService = scope.ServiceProvider.GetRequiredService<IComposeUpdateService>();
            IComposeFileCacheService fileCacheService = scope.ServiceProvider.GetRequiredService<IComposeFileCacheService>();
            IAuditService auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            _logger.LogInformation("AutoUpdateCompose cycle started (trigger: {Trigger})", trigger);
            await auditService.LogActionAsync(
                userId: null,
                action: AuditActions.AutoUpdateComposeStarted,
                ipAddress: "system",
                details: $"Trigger: {trigger}",
                resourceType: "auto_update",
                resourceId: "compose"
            );

            // Use cached check results (forceRefresh: false) to avoid hammering registries
            // with ~N manifest requests every cycle. ProjectUpdateCheckBackgroundService keeps
            // the cache warm; uncached projects are still checked on demand. This sharply cuts
            // Docker Hub anonymous-rate-limit pressure during the bulk update that follows.
            DTOs.CheckAllUpdatesResponse checkResult = await composeUpdateService.CheckAllProjectsUpdatesAsync(userId: 1, forceRefresh: false, ct);

            List<DiscoveredComposeFile> files = await fileCacheService.GetOrScanAsync();
            Dictionary<string, DiscoveredComposeFile> filesByProject = files
                .GroupBy(f => f.ProjectName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int updated = 0;
            int skippedFlag = 0;

            foreach (DTOs.ProjectUpdateSummary summary in checkResult.Projects)
            {
                if (ct.IsCancellationRequested) break;
                if (summary.ServicesWithUpdates <= 0) continue;

                if (filesByProject.TryGetValue(summary.ProjectName, out DiscoveredComposeFile? file)
                    && !file.AutoUpdateEnabled)
                {
                    skippedFlag++;
                    _logger.LogInformation("Skipping auto-update for project {Project}: x-auto-update is false", summary.ProjectName);
                    await auditService.LogActionAsync(
                        userId: null,
                        action: AuditActions.AutoUpdateComposeSkipped,
                        ipAddress: "system",
                        details: "Project marked with x-auto-update: false",
                        resourceType: "compose_project",
                        resourceId: summary.ProjectName
                    );
                    continue;
                }

                try
                {
                    _logger.LogInformation("Auto-updating project {Project} ({Count} services with updates)",
                        summary.ProjectName, summary.ServicesWithUpdates);

                    DTOs.UpdateTriggerResponse response = await composeUpdateService.UpdateProjectAsync(
                        projectName: summary.ProjectName,
                        services: null,
                        updateAll: true,
                        restartFullProject: true,
                        restartAfterUpdate: true,
                        userId: 1,
                        ipAddress: "system",
                        ct: ct);

                    if (response.Success)
                    {
                        updated++;
                        await auditService.LogActionAsync(
                            userId: null,
                            action: AuditActions.AutoUpdateComposeProjectUpdated,
                            ipAddress: "system",
                            details: $"Operation: {response.OperationId}",
                            resourceType: "compose_project",
                            resourceId: summary.ProjectName
                        );
                    }
                    else
                    {
                        _logger.LogWarning("Auto-update returned non-success for project {Project}: {Message}",
                            summary.ProjectName, response.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-updating project {Project}", summary.ProjectName);
                }
            }

            _logger.LogInformation(
                "AutoUpdateCompose cycle complete: {Updated} updated, {SkippedFlag} skipped via x-auto-update, {Total} projects checked",
                updated, skippedFlag, checkResult.ProjectsChecked);
        }
        finally
        {
            _cycleLock.Release();
        }
    }

    private async Task ConsumePendingAutoUpdateFlagAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AppSetting? pending = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == PendingKey, ct);
            bool isPending = pending != null
                && bool.TryParse(pending.Value, out bool b)
                && b;

            if (!isPending) return;

            _logger.LogInformation("PendingComposeAutoUpdate flag detected, running an immediate compose auto-update cycle");

            if (pending != null)
            {
                pending.Value = "false";
                await db.SaveChangesAsync(ct);
            }

            await RunCycleAsync("pending-after-restart", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read/consume PendingComposeAutoUpdate flag");
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
            _logger.LogWarning(ex, "Failed to read AutoUpdateCompose settings; treating as disabled");
            return (false, DefaultCron);
        }
    }

    private static async Task SetPendingAutoUpdateFlagAsync(IServiceScope scope, bool value, CancellationToken ct)
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == PendingKey, ct);
        if (setting == null)
        {
            setting = new AppSetting { Key = PendingKey, Value = value ? "true" : "false" };
            db.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = value ? "true" : "false";
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task LogAuditSafelyAsync(IServiceScope scope, string action, string details)
    {
        try
        {
            IAuditService audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await audit.LogActionAsync(
                userId: null,
                action: action,
                ipAddress: "system",
                details: details,
                resourceType: "auto_update",
                resourceId: "compose");
        }
        catch
        {
            // Best effort
        }
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
