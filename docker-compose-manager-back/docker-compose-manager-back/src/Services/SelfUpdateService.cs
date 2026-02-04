using Docker.DotNet;
using Docker.DotNet.Models;
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
    private const string UpdaterImage = "docker:cli";

    private readonly IGitHubReleaseService _gitHubReleaseService;
    private readonly IComposeFileDetectorService _composeFileDetector;
    private readonly IPathMappingService _pathMappingService;
    private readonly DockerCommandExecutor _dockerCommandExecutor;
    private readonly DockerClient? _dockerClient;
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
        IConfiguration configuration,
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

        // Initialize Docker client for launching updater container
        string? dockerHost = configuration["Docker:Host"];
        if (!string.IsNullOrEmpty(dockerHost))
        {
            try
            {
                _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
                _logger.LogDebug("SelfUpdateService Docker client initialized with host: {DockerHost}", dockerHost);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Docker client for self-update. Update via updater container will not be available.");
                _dockerClient = null;
            }
        }
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
            // Resolve compose file paths: configuration takes priority, then auto-detection
            ComposeFilePaths? paths = await ResolveComposeFilePathsAsync();
            if (paths == null)
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

            _logger.LogInformation("Starting application update from {Current} to {Latest} using compose file: {ComposeFile} (Host: {HostPath})",
                updateInfo.CurrentVersion, updateInfo.LatestVersion, paths.ContainerPath, paths.HostPath);

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
            _ = ExecuteUpdateAsync(operationId, paths, cancellationToken);

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
    /// Contains resolved compose file paths for both container and host.
    /// </summary>
    private record ComposeFilePaths(string ContainerPath, string HostPath, string? HostWorkingDirectory);

    /// <summary>
    /// Resolves the compose file path using configuration (priority) or auto-detection.
    /// Returns both container path (for pull) and host path (for updater container).
    /// </summary>
    private async Task<ComposeFilePaths?> ResolveComposeFilePathsAsync()
    {
        // Priority 1: Configuration (environment variable or appsettings)
        if (!string.IsNullOrWhiteSpace(_selfUpdateOptions.ComposeFilePath))
        {
            _logger.LogDebug("Using configured compose file path: {Path}", _selfUpdateOptions.ComposeFilePath);
            // When configured, assume it's the container path and we need to detect host path
            ComposeDetectionResult detection = await _composeFileDetector.GetComposeDetectionResultAsync();
            string hostPath = detection.ComposeFilePath ?? _selfUpdateOptions.ComposeFilePath;
            string? hostWorkingDir = detection.WorkingDirectory ?? Path.GetDirectoryName(hostPath);
            return new ComposeFilePaths(_selfUpdateOptions.ComposeFilePath, hostPath, hostWorkingDir);
        }

        // Priority 2: Auto-detection from Docker labels
        _logger.LogDebug("Compose file path not configured, attempting auto-detection...");
        ComposeDetectionResult detectionResult = await _composeFileDetector.GetComposeDetectionResultAsync();

        if (detectionResult.IsRunningViaCompose && !string.IsNullOrEmpty(detectionResult.ComposeFilePath))
        {
            // The detected path is a host path (from Docker labels), convert it to container path
            string hostPath = detectionResult.ComposeFilePath;
            string? containerPath = _pathMappingService.ConvertHostPathToContainerPath(hostPath);

            if (!string.IsNullOrEmpty(containerPath))
            {
                _logger.LogDebug("Auto-detected compose file paths: Host={HostPath}, Container={ContainerPath} (Project: {Project})",
                    hostPath, containerPath, detectionResult.ProjectName);
                return new ComposeFilePaths(containerPath, hostPath, detectionResult.WorkingDirectory);
            }

            _logger.LogWarning(
                "Auto-detection found compose file at host path {HostPath}, but could not convert to container path. " +
                "Consider setting ComposeDiscovery:HostPathMapping in configuration.",
                hostPath);
            return null;
        }

        // Auto-detection failed
        if (!detectionResult.IsRunningInDocker)
        {
            _logger.LogWarning("Auto-detection failed: Application is not running in a Docker container");
        }
        else if (!detectionResult.IsRunningViaCompose)
        {
            _logger.LogWarning("Auto-detection failed: Container was not started via docker-compose. Error: {Error}",
                detectionResult.DetectionError);
        }

        return null;
    }

    private async Task ExecuteUpdateAsync(string operationId, ComposeFilePaths paths, CancellationToken cancellationToken)
    {
        try
        {
            string containerWorkingDirectory = Path.GetDirectoryName(paths.ContainerPath) ?? "/app";
            string composeFileName = Path.GetFileName(paths.ContainerPath);

            _logger.LogDebug("Executing docker compose pull for update, compose file: {ComposeFile}", paths.ContainerPath);

            // Step 1: Pull the latest image (from current container - this doesn't restart anything)
            (int pullExitCode, string pullOutput, string pullError) = await _dockerCommandExecutor.ExecuteComposeCommandAsync(
                workingDirectory: containerWorkingDirectory,
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

            // Step 2: Launch an updater container to run docker compose up
            // This is necessary because the current container will be killed during the update,
            // which would also kill any child process running docker compose.
            // By running it in a separate container, the update process survives.
            _logger.LogInformation("Launching updater container to recreate application");

            await LaunchUpdaterContainerAsync(paths);

            _logger.LogInformation("Updater container launched. Application will restart shortly.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update execution");
            lock (_updateLock) { _updateInProgress = false; }
        }
    }

    /// <summary>
    /// Launches an ephemeral Docker container that runs docker compose up to update this application.
    /// The container runs on the host via Docker socket and survives the restart of this container.
    /// </summary>
    private async Task LaunchUpdaterContainerAsync(ComposeFilePaths paths)
    {
        if (_dockerClient == null)
        {
            throw new InvalidOperationException("Docker client is not available. Cannot launch updater container.");
        }

        string hostWorkingDirectory = paths.HostWorkingDirectory ?? Path.GetDirectoryName(paths.HostPath) ?? "/";
        string composeFileName = Path.GetFileName(paths.HostPath);

        _logger.LogDebug("Launching updater container with working directory: {WorkingDir}, compose file: {ComposeFile}",
            hostWorkingDirectory, composeFileName);

        // Ensure the updater image is available
        // Note: FromImage should be "docker" and Tag should be "cli" for docker:cli
        try
        {
            await _dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = "docker", Tag = "cli" },
                null,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                    {
                        _logger.LogDebug("Pulling updater image: {Status}", msg.Status);
                    }
                })
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pull updater image {Image}. Attempting to use existing image.", UpdaterImage);
        }

        // Create the updater container
        CreateContainerResponse container = await _dockerClient.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = UpdaterImage,
            Name = $"dcm-updater-{Guid.NewGuid():N}",
            WorkingDir = hostWorkingDirectory,
            Cmd = new[]
            {
                "docker", "compose", "-f", composeFileName, "up", "-d", "--force-recreate"
            },
            HostConfig = new HostConfig
            {
                // Mount Docker socket so the container can communicate with Docker daemon
                Binds = new[]
                {
                    "/var/run/docker.sock:/var/run/docker.sock",
                    $"{hostWorkingDirectory}:{hostWorkingDirectory}"
                },
                // Automatically remove the container when it exits
                AutoRemove = true
            },
            Labels = new Dictionary<string, string>
            {
                ["docker-compose-manager.updater"] = "true",
                ["docker-compose-manager.update-target"] = paths.HostPath
            }
        });

        _logger.LogInformation("Created updater container {ContainerId}", container.ID);

        // Start the container
        bool started = await _dockerClient.Containers.StartContainerAsync(container.ID, null);

        if (started)
        {
            _logger.LogInformation("Updater container started successfully. Container will restart shortly.");
        }
        else
        {
            _logger.LogError("Failed to start updater container");
            throw new InvalidOperationException("Failed to start updater container");
        }
    }
}
