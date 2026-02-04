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
    private readonly IComposeFileDetectorService _composeFileDetector;
    private readonly IPathMappingService _pathMappingService;
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
        IComposeFileDetectorService composeFileDetector,
        IPathMappingService pathMappingService,
        DockerCommandExecutor dockerCommandExecutor,
        IAuditService auditService,
        IHubContext<OperationsHub> hubContext,
        IOptions<SelfUpdateOptions> selfUpdateOptions,
        IOptions<MaintenanceOptions> maintenanceOptions,
        ILogger<SelfUpdateService> logger)
    {
        _gitHubReleaseService = gitHubReleaseService;
        _composeFileDetector = composeFileDetector;
        _pathMappingService = pathMappingService;
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
            _logger.LogDebug("Checking for application updates");
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
            // Resolve compose file path: configuration takes priority, then auto-detection
            string? composeFilePath = await ResolveComposeFilePathAsync();
            if (string.IsNullOrEmpty(composeFilePath))
            {
                lock (_updateLock) { _updateInProgress = false; }
                return new UpdateTriggerResponse(false,
                    "Cannot perform self-update: compose file path not configured and auto-detection failed. " +
                    "Please set the SelfUpdate:ComposeFilePath configuration or run the application via docker-compose.", null);
            }

            // Check if update is actually available
            AppUpdateCheckResponse updateInfo = await _gitHubReleaseService.CheckForUpdateAsync(cancellationToken);

            if (!updateInfo.UpdateAvailable)
            {
                lock (_updateLock) { _updateInProgress = false; }
                return new UpdateTriggerResponse(false, "No update available. Current version is already the latest.", null);
            }

            _logger.LogInformation("Starting application update from {Current} to {Latest} using compose file: {ComposeFile}",
                updateInfo.CurrentVersion, updateInfo.LatestVersion, composeFilePath);

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
            _logger.LogDebug("Broadcasting maintenance mode notification");
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
            _ = ExecuteUpdateAsync(operationId, composeFilePath, cancellationToken);

            return new UpdateTriggerResponse(true, "Update started. The application will restart shortly.", operationId);
        }
        catch (Exception ex)
        {
            lock (_updateLock) { _updateInProgress = false; }
            _logger.LogError(ex, "Error triggering update");
            return new UpdateTriggerResponse(false, $"Failed to trigger update: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Resolves the compose file path using configuration (priority) or auto-detection.
    /// When auto-detecting, converts host paths to container paths using PathMappingService.
    /// </summary>
    private async Task<string?> ResolveComposeFilePathAsync()
    {
        // Priority 1: Configuration (environment variable or appsettings)
        if (!string.IsNullOrWhiteSpace(_selfUpdateOptions.ComposeFilePath))
        {
            _logger.LogDebug("Using configured compose file path: {Path}", _selfUpdateOptions.ComposeFilePath);
            return _selfUpdateOptions.ComposeFilePath;
        }

        // Priority 2: Auto-detection from Docker labels
        _logger.LogDebug("Compose file path not configured, attempting auto-detection...");
        ComposeDetectionResult detection = await _composeFileDetector.GetComposeDetectionResultAsync();

        if (detection.IsRunningViaCompose && !string.IsNullOrEmpty(detection.ComposeFilePath))
        {
            // The detected path is a host path (from Docker labels), convert it to container path
            string hostPath = detection.ComposeFilePath;
            string? containerPath = _pathMappingService.ConvertHostPathToContainerPath(hostPath);

            if (!string.IsNullOrEmpty(containerPath))
            {
                _logger.LogDebug("Auto-detected compose file path: {HostPath} -> {ContainerPath} (Project: {Project})",
                    hostPath, containerPath, detection.ProjectName);
                return containerPath;
            }

            _logger.LogWarning(
                "Auto-detection found compose file at host path {HostPath}, but could not convert to container path. " +
                "Consider setting ComposeDiscovery:HostPathMapping in configuration.",
                hostPath);
            return null;
        }

        // Auto-detection failed
        if (!detection.IsRunningInDocker)
        {
            _logger.LogWarning("Auto-detection failed: Application is not running in a Docker container");
        }
        else if (!detection.IsRunningViaCompose)
        {
            _logger.LogWarning("Auto-detection failed: Container was not started via docker-compose. Error: {Error}",
                detection.DetectionError);
        }

        return null;
    }

    private async Task ExecuteUpdateAsync(string operationId, string composeFilePath, CancellationToken cancellationToken)
    {
        try
        {
            string workingDirectory = Path.GetDirectoryName(composeFilePath) ?? "/app";
            string composeFileName = Path.GetFileName(composeFilePath);

            _logger.LogDebug("Executing docker compose pull for update, compose file: {ComposeFile}", composeFilePath);

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

            _logger.LogDebug("Docker compose pull succeeded, output: {Output}", pullOutput);

            // Recreate containers with new image
            _logger.LogDebug("Executing docker compose up -d for update");

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
                _logger.LogDebug("Docker compose up succeeded, output: {Output}", upOutput);
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
