using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.Data;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Background service that periodically checks for compose project updates
/// and broadcasts results via SSE.
/// </summary>
public class ProjectUpdateCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SseConnectionManagerService _sseManager;
    private readonly ILogger<ProjectUpdateCheckBackgroundService> _logger;
    private readonly UpdateCheckOptions _options;

    public ProjectUpdateCheckBackgroundService(
        IServiceProvider serviceProvider,
        SseConnectionManagerService sseManager,
        IOptions<UpdateCheckOptions> options,
        ILogger<ProjectUpdateCheckBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _sseManager = sseManager;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Periodic project update check is disabled");
            return;
        }

        _logger.LogInformation(
            "ProjectUpdateCheckBackgroundService starting (delay: {DelaySeconds}s, interval: {IntervalMinutes}min)",
            _options.StartupDelaySeconds,
            _options.CheckIntervalMinutes);

        // Wait before the first check
        await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProjectsAndBroadcastAsync("Periodic", stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic project update check");
            }

            // Read interval from DB (AppSettings), fallback to config
            int intervalMinutes = await GetCheckIntervalAsync(stoppingToken);
            _logger.LogDebug("Next periodic update check in {IntervalMinutes} minutes", intervalMinutes);

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }

        _logger.LogInformation("ProjectUpdateCheckBackgroundService stopped");
    }

    /// <summary>
    /// Performs the bulk update check and broadcasts the result via SSE.
    /// </summary>
    public async Task CheckAllProjectsAndBroadcastAsync(string trigger, CancellationToken ct)
    {
        _logger.LogInformation("Starting {Trigger} project update check", trigger);

        using IServiceScope scope = _serviceProvider.CreateScope();
        IComposeUpdateService updateService = scope.ServiceProvider.GetRequiredService<IComposeUpdateService>();

        CheckAllUpdatesResponse result = await updateService.CheckAllProjectsUpdatesAsync(userId: 1, ct);

        ProjectUpdatesCheckedEvent sseEvent = new(
            Projects: result.Projects,
            ProjectsChecked: result.ProjectsChecked,
            ProjectsWithUpdates: result.ProjectsWithUpdates,
            TotalServicesWithUpdates: result.TotalServicesWithUpdates,
            CheckedAt: result.CheckedAt,
            Trigger: trigger.ToLower()
        );

        await _sseManager.BroadcastAsync("ProjectUpdatesChecked", sseEvent);

        _logger.LogInformation(
            "{Trigger} project update check complete: {Checked} projects checked, {WithUpdates} with updates",
            trigger,
            result.ProjectsChecked,
            result.ProjectsWithUpdates);

        // Now check containers — compose-managed ones will be resolved from project cache (no extra registry calls),
        // only standalone containers will trigger actual registry checks.
        try
        {
            IContainerUpdateService containerUpdateService = scope.ServiceProvider.GetRequiredService<IContainerUpdateService>();
            await containerUpdateService.CheckAllContainerUpdatesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during container update check after project check");
        }
    }

    private async Task<int> GetCheckIntervalAsync(CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            AppSetting? setting = await dbContext.AppSettings
                .FirstOrDefaultAsync(s => s.Key == "ProjectUpdateCheckIntervalMinutes", ct);

            if (setting != null && int.TryParse(setting.Value, out int interval) && interval >= 5)
            {
                return interval;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read check interval from database, using config default");
        }

        return _options.CheckIntervalMinutes;
    }
}
