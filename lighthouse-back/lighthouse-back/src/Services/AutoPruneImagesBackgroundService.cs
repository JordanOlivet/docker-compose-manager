using Cronos;
using Lighthouse.Data;
using Lighthouse.DTOs;
using Lighthouse.Models;
using Lighthouse.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Lighthouse.Services;

/// <summary>
/// Background service that prunes unused Docker images on a cron schedule.
/// Disabled by default. Prunes dangling images only unless configured otherwise.
/// Skips a cycle while a self-update is in progress (the new image may not yet be
/// referenced by a container and could otherwise be pruned).
/// </summary>
public class AutoPruneImagesBackgroundService : BackgroundService
{
    public const string EnabledKey = "AutoPruneImagesEnabled";
    public const string CronKey = "AutoPruneImagesCron";
    public const string DanglingOnlyKey = "AutoPruneImagesDanglingOnly";
    public const string DefaultCron = "0 3 * * *";

    // Short fixed tick: every tick we check whether a cron occurrence fell within the
    // elapsed window. This never misses the scheduled minute and picks up enable/cron
    // changes within one tick.
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);

    private readonly IServiceProvider _serviceProvider;
    private readonly SseConnectionManagerService _sse;
    private readonly ILogger<AutoPruneImagesBackgroundService> _logger;
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    public AutoPruneImagesBackgroundService(
        IServiceProvider serviceProvider,
        SseConnectionManagerService sse,
        ILogger<AutoPruneImagesBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _sse = sse;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoPruneImagesBackgroundService starting");

        DateTime lastCheckUtc = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TickInterval, stoppingToken);

                (bool enabled, string cron, bool danglingOnly) = await ReadSettingsAsync(stoppingToken);
                DateTime nowUtc = DateTime.UtcNow;

                if (!enabled)
                {
                    lastCheckUtc = nowUtc;
                    continue;
                }

                if (!TryParseCron(cron, out CronExpression? expression))
                {
                    _logger.LogWarning("Invalid cron expression '{Cron}' for AutoPruneImages.", cron);
                    lastCheckUtc = nowUtc;
                    continue;
                }

                DateTime? occurrence = expression!.GetNextOccurrence(lastCheckUtc, inclusive: false);
                if (occurrence.HasValue && occurrence.Value <= nowUtc)
                {
                    _logger.LogInformation("AutoPruneImages: scheduled occurrence {Occurrence:O} is due, running cycle", occurrence.Value);
                    await RunCycleAsync(danglingOnly, stoppingToken);
                }

                lastCheckUtc = nowUtc;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AutoPruneImagesBackgroundService loop");
            }
        }

        _logger.LogInformation("AutoPruneImagesBackgroundService stopped");
    }

    private async Task RunCycleAsync(bool danglingOnly, CancellationToken ct)
    {
        if (!await _cycleLock.WaitAsync(0, ct))
        {
            _logger.LogDebug("Auto-prune cycle already in progress, skipping");
            return;
        }

        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();

            ISelfUpdateService selfUpdateService = scope.ServiceProvider.GetRequiredService<ISelfUpdateService>();
            if (selfUpdateService.IsUpdateInProgress)
            {
                _logger.LogInformation("Self-update in progress, deferring auto-prune cycle");
                return;
            }

            IImageService imageService = scope.ServiceProvider.GetRequiredService<IImageService>();
            IAuditService auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            INotificationService notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            PruneImagesResultDto result = await imageService.PruneImagesAsync(danglingOnly, ct);

            _logger.LogInformation(
                "AutoPruneImages cycle complete: {Count} image(s) removed, {Bytes} bytes reclaimed (danglingOnly={DanglingOnly})",
                result.ImagesDeleted.Count, result.SpaceReclaimed, danglingOnly);

            await auditService.LogActionAsync(
                userId: null,
                action: AuditActions.AutoPruneImages,
                ipAddress: "system",
                details: $"Removed {result.ImagesDeleted.Count} image(s), reclaimed {result.SpaceReclaimed} bytes (danglingOnly={danglingOnly})",
                resourceType: "image");

            if (result.ImagesDeleted.Count > 0)
            {
                await _sse.BroadcastAsync("ImagesChanged", new
                {
                    action = "prune",
                    timestamp = DateTime.UtcNow
                });

                // Best-effort Discord summary, mirroring the compose auto-update flow.
                await notificationService.NotifyImagePruneAsync(
                    result.ImagesDeleted, result.SpaceReclaimed, danglingOnly, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running auto-prune cycle");
        }
        finally
        {
            _cycleLock.Release();
        }
    }

    private async Task<(bool Enabled, string Cron, bool DanglingOnly)> ReadSettingsAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            AppSetting? enabledSetting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == EnabledKey, ct);
            AppSetting? cronSetting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == CronKey, ct);
            AppSetting? danglingSetting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == DanglingOnlyKey, ct);

            bool enabled = enabledSetting != null
                && bool.TryParse(enabledSetting.Value, out bool b)
                && b;

            string cron = !string.IsNullOrWhiteSpace(cronSetting?.Value) ? cronSetting!.Value : DefaultCron;

            // Default to dangling-only (safe) when the setting is absent.
            bool danglingOnly = danglingSetting == null
                || !bool.TryParse(danglingSetting.Value, out bool d)
                || d;

            return (enabled, cron, danglingOnly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read AutoPruneImages settings; treating as disabled");
            return (false, DefaultCron, true);
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
