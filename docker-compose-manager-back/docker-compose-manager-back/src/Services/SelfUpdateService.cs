using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace docker_compose_manager_back.Services;

public interface ISelfUpdateService
{
    Task<AppUpdateCheckResponse> CheckUpdateAsync(CancellationToken cancellationToken = default);
    Task<UpdateTriggerResponse> TriggerUpdateAsync(int userId, string ipAddress, CancellationToken cancellationToken = default);
    bool IsUpdateInProgress { get; }
}

public class SelfUpdateService : ISelfUpdateService
{
    private readonly IGitHubReleaseService _gitHubReleaseService;
    private readonly DockerCommandExecutor _dockerCommandExecutor;
    private readonly IAuditService _auditService;
    private readonly IHubContext<OperationsHub> _hubContext;
    private readonly SelfUpdateOptions _selfUpdateOptions;
    private readonly MaintenanceOptions _maintenanceOptions;
    private readonly ILogger<SelfUpdateService> _logger;

    private bool _updateInProgress;
    private readonly object _updateLock = new();

    public bool IsUpdateInProgress
    {
        get
        {
            lock (_updateLock)
            {
                return _updateInProgress;
            }
        }
    }

    public SelfUpdateService(
        IGitHubReleaseService gitHubReleaseService,
        DockerCommandExecutor dockerCommandExecutor,
        IAuditService auditService,
        IHubContext<OperationsHub> hubContext,
        IOptions<SelfUpdateOptions> selfUpdateOptions,
        IOptions<MaintenanceOptions> maintenanceOptions,
        ILogger<SelfUpdateService> logger)
    {
        _gitHubReleaseService = gitHubReleaseService;
        _dockerCommandExecutor = dockerCommandExecutor;
        _auditService = auditService;
        _hubContext = hubContext;
        _selfUpdateOptions = selfUpdateOptions.Value;
        _maintenanceOptions = maintenanceOptions.Value;
        _logger = logger;
    }

    public async Task<AppUpdateCheckResponse> CheckUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!_selfUpdateOptions.Enabled)
        {
            _logger.LogWarning("Self-update feature is disabled");
            throw new InvalidOperationException("Self-update feature is disabled in configuration");
        }

        try
        {
            _logger.LogInformation("Checking for application updates");
            return await _gitHubReleaseService.CheckForUpdateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            throw;
        }
    }

    public async Task<UpdateTriggerResponse> TriggerUpdateAsync(int userId, string ipAddress, CancellationToken cancellationToken = default)
    {
        if (!_selfUpdateOptions.Enabled)
        {
            _logger.LogWarning("Self-update feature is disabled");
            return new UpdateTriggerResponse(false, "Self-update feature is disabled in configuration", null);
        }

        lock (_updateLock)
        {
            if (_updateInProgress)
            {
                return new UpdateTriggerResponse(false, "An update is already in progress", null);
            }
            _updateInProgress = true;
        }

        try
        {
            // Check if update is actually available
            AppUpdateCheckResponse updateInfo = await _gitHubReleaseService.CheckForUpdateAsync(cancellationToken);

            if (!updateInfo.UpdateAvailable)
            {
                lock (_updateLock) { _updateInProgress = false; }
                return new UpdateTriggerResponse(false, "No update available. Current version is already the latest.", null);
            }

            _logger.LogInformation("Starting application update from {Current} to {Latest}",
                updateInfo.CurrentVersion, updateInfo.LatestVersion);

            // Log the update action
            await _auditService.LogActionAsync(
                userId: userId,
                action: AuditActions.AppUpdate,
                ipAddress: ipAddress,
                details: $"Updating application from {updateInfo.CurrentVersion} to {updateInfo.LatestVersion}",
                resourceType: "application",
                resourceId: "self",
                before: new { Version = updateInfo.CurrentVersion },
                after: new { Version = updateInfo.LatestVersion }
            );

            // Notify all clients about maintenance mode
            _logger.LogInformation("Broadcasting maintenance mode notification");
            var notification = new MaintenanceModeNotification(
                IsActive: true,
                Message: $"Application is updating to version {updateInfo.LatestVersion}. Please wait...",
                EstimatedEndTime: DateTime.UtcNow.AddMinutes(2),
                GracePeriodSeconds: _maintenanceOptions.GracePeriodSeconds
            );

            await _hubContext.Clients.All.SendAsync("MaintenanceMode", notification, cancellationToken);

            // Wait for grace period to allow clients to prepare
            await Task.Delay(_maintenanceOptions.GracePeriodSeconds * 1000, cancellationToken);

            // Execute the update
            string operationId = Guid.NewGuid().ToString();
            _ = ExecuteUpdateAsync(operationId, cancellationToken);

            return new UpdateTriggerResponse(true, "Update started. The application will restart shortly.", operationId);
        }
        catch (Exception ex)
        {
            lock (_updateLock) { _updateInProgress = false; }
            _logger.LogError(ex, "Error triggering update");
            return new UpdateTriggerResponse(false, $"Failed to trigger update: {ex.Message}", null);
        }
    }

    private async Task ExecuteUpdateAsync(string operationId, CancellationToken cancellationToken)
    {
        try
        {
            string composeFilePath = _selfUpdateOptions.ComposeFilePath;
            string workingDirectory = Path.GetDirectoryName(composeFilePath) ?? "/app";
            string composeFileName = Path.GetFileName(composeFilePath);

            _logger.LogInformation("Executing docker compose pull for update, compose file: {ComposeFile}", composeFilePath);

            // Pull the latest image
            (int pullExitCode, string pullOutput, string pullError) = await _dockerCommandExecutor.ExecuteComposeCommandAsync(
                workingDirectory: workingDirectory,
                arguments: "pull",
                composeFile: composeFileName,
                cancellationToken: cancellationToken
            );

            if (pullExitCode != 0)
            {
                _logger.LogError("Docker compose pull failed: {Error}", pullError);
                lock (_updateLock) { _updateInProgress = false; }

                // Notify clients that update failed
                await _hubContext.Clients.All.SendAsync("MaintenanceMode", new MaintenanceModeNotification(
                    IsActive: false,
                    Message: "Update failed. Please try again later.",
                    EstimatedEndTime: null,
                    GracePeriodSeconds: 0
                ), cancellationToken);

                return;
            }

            _logger.LogInformation("Docker compose pull succeeded, output: {Output}", pullOutput);

            // Recreate containers with new image
            _logger.LogInformation("Executing docker compose up -d for update");

            (int upExitCode, string upOutput, string upError) = await _dockerCommandExecutor.ExecuteComposeCommandAsync(
                workingDirectory: workingDirectory,
                arguments: "up -d --force-recreate",
                composeFile: composeFileName,
                cancellationToken: cancellationToken
            );

            if (upExitCode != 0)
            {
                _logger.LogError("Docker compose up failed: {Error}", upError);
                // At this point we might still restart, log the error
            }
            else
            {
                _logger.LogInformation("Docker compose up succeeded, output: {Output}", upOutput);
            }

            // The application should restart here due to container recreation
            // If we reach this point, something might be wrong
            _logger.LogWarning("Update command completed but application did not restart. This may indicate an issue.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update execution");
            lock (_updateLock) { _updateInProgress = false; }
        }
    }
}
