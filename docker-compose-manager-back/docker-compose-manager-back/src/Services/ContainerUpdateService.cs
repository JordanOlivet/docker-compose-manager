using docker_compose_manager_back.DTOs;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for checking and performing container-level updates (both compose-managed and standalone).
/// </summary>
public interface IContainerUpdateService
{
    /// <summary>
    /// Checks if an update is available for a specific container's image.
    /// </summary>
    Task<ContainerUpdateCheckResponse> CheckContainerUpdateAsync(string containerId, CancellationToken ct = default);

    /// <summary>
    /// Updates a standalone container by pulling new image and recreating with same config.
    /// For compose-managed containers, delegates to ComposeUpdateService.
    /// </summary>
    Task<UpdateTriggerResponse> UpdateContainerAsync(string containerId, int userId, string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Checks all containers for updates and broadcasts results via SSE.
    /// </summary>
    Task<ContainerUpdatesCheckedEvent> CheckAllContainerUpdatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns cached container update summaries (does not trigger new checks).
    /// </summary>
    List<ContainerUpdateSummary> GetCachedContainerUpdateStatus();
}

public class ContainerUpdateService : IContainerUpdateService
{
    private readonly DockerService _dockerService;
    private readonly IImageDigestService _imageDigestService;
    private readonly IImageUpdateCacheService _updateCacheService;
    private readonly IContainerUpdateCacheService _containerUpdateCacheService;
    private readonly IComposeUpdateService _composeUpdateService;
    private readonly DockerCommandExecutorService _dockerExecutor;
    private readonly DockerPullProgressParser _progressParser;
    private readonly OperationService _operationService;
    private readonly SseConnectionManagerService _sseManager;
    private readonly IAuditService _auditService;
    private readonly ILogger<ContainerUpdateService> _logger;

    public ContainerUpdateService(
        DockerService dockerService,
        IImageDigestService imageDigestService,
        IImageUpdateCacheService updateCacheService,
        IContainerUpdateCacheService containerUpdateCacheService,
        IComposeUpdateService composeUpdateService,
        DockerCommandExecutorService dockerExecutor,
        DockerPullProgressParser progressParser,
        OperationService operationService,
        SseConnectionManagerService sseManager,
        IAuditService auditService,
        ILogger<ContainerUpdateService> logger)
    {
        _dockerService = dockerService;
        _imageDigestService = imageDigestService;
        _updateCacheService = updateCacheService;
        _containerUpdateCacheService = containerUpdateCacheService;
        _composeUpdateService = composeUpdateService;
        _dockerExecutor = dockerExecutor;
        _progressParser = progressParser;
        _operationService = operationService;
        _sseManager = sseManager;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ContainerUpdateCheckResponse> CheckContainerUpdateAsync(string containerId, CancellationToken ct = default)
    {
        ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(containerId);
        if (container == null)
        {
            return new ContainerUpdateCheckResponse(
                ContainerId: containerId,
                ContainerName: "",
                Image: "",
                UpdateAvailable: false,
                IsComposeManaged: false,
                ProjectName: null,
                LocalDigest: null,
                RemoteDigest: null,
                RequiredPull: false,
                Error: "Container not found"
            );
        }

        string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");
        string? serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
        bool isComposeManaged = !string.IsNullOrEmpty(projectName);

        try
        {
            // For compose-managed containers, check if we have cached results
            if (isComposeManaged && !string.IsNullOrEmpty(projectName))
            {
                ProjectUpdateCheckResponse? cached = _updateCacheService.GetCachedCheck(projectName);
                if (cached != null && !string.IsNullOrEmpty(serviceName))
                {
                    ImageUpdateStatus? serviceStatus = cached.Images
                        .FirstOrDefault(i => i.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                    if (serviceStatus != null)
                    {
                        var cachedResult = new ContainerUpdateCheckResponse(
                            ContainerId: container.Id,
                            ContainerName: container.Name,
                            Image: container.Image,
                            UpdateAvailable: serviceStatus.UpdateAvailable,
                            IsComposeManaged: true,
                            ProjectName: projectName,
                            LocalDigest: serviceStatus.LocalDigest,
                            RemoteDigest: serviceStatus.RemoteDigest,
                            RequiredPull: false,
                            Error: serviceStatus.Error
                        );
                        _containerUpdateCacheService.SetCachedCheck(container.Id, cachedResult);
                        return cachedResult;
                    }
                }
            }

            // Check via registry API
            ImageUpdateStatus status = await _imageDigestService.CheckImageUpdateAsync(
                container.Image,
                serviceName ?? container.Name,
                ct);

            var result = new ContainerUpdateCheckResponse(
                ContainerId: container.Id,
                ContainerName: container.Name,
                Image: container.Image,
                UpdateAvailable: status.UpdateAvailable,
                IsComposeManaged: isComposeManaged,
                ProjectName: projectName,
                LocalDigest: status.LocalDigest,
                RemoteDigest: status.RemoteDigest,
                RequiredPull: false,
                Error: status.Error
            );

            _containerUpdateCacheService.SetCachedCheck(container.Id, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking update for container {ContainerId}", containerId);
            return new ContainerUpdateCheckResponse(
                ContainerId: container.Id,
                ContainerName: container.Name,
                Image: container.Image,
                UpdateAvailable: false,
                IsComposeManaged: isComposeManaged,
                ProjectName: projectName,
                LocalDigest: null,
                RemoteDigest: null,
                RequiredPull: false,
                Error: ex.Message
            );
        }
    }

    public async Task<UpdateTriggerResponse> UpdateContainerAsync(string containerId, int userId, string ipAddress, CancellationToken ct = default)
    {
        ContainerDetailsDto? container = await _dockerService.GetContainerDetailsAsync(containerId);
        if (container == null)
        {
            return new UpdateTriggerResponse(false, "Container not found", null);
        }

        string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");
        bool isComposeManaged = !string.IsNullOrEmpty(projectName);

        if (isComposeManaged && projectName != null)
        {
            // Delegate to compose update service
            string? serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
            List<string>? services = serviceName != null ? new List<string> { serviceName } : null;

            UpdateTriggerResponse composeResult = await _composeUpdateService.UpdateProjectAsync(
                projectName,
                services,
                services == null,
                false, // Don't restart full project for single container updates
                userId,
                ipAddress,
                ct);

            if (composeResult.Success)
                _containerUpdateCacheService.InvalidateContainer(containerId);

            return composeResult;
        }

        // Standalone container update
        UpdateTriggerResponse standaloneResult = await UpdateStandaloneContainerAsync(container, userId, ipAddress, ct);

        if (standaloneResult.Success)
            _containerUpdateCacheService.InvalidateContainer(containerId);

        return standaloneResult;
    }

    private async Task<UpdateTriggerResponse> UpdateStandaloneContainerAsync(
        ContainerDetailsDto container,
        int userId,
        string ipAddress,
        CancellationToken ct)
    {
        string operationId = Guid.NewGuid().ToString();
        string containerName = container.Name.TrimStart('/');

        try
        {
            _logger.LogInformation("Updating standalone container {ContainerName} ({Image})", containerName, container.Image);

            // Initialize progress tracking with single service entry
            _progressParser.Reset();
            Dictionary<string, ServicePullProgress> serviceProgress = _progressParser.InitializeProgress(new[] { containerName });
            DateTime lastProgressSent = DateTime.MinValue;
            TimeSpan minProgressInterval = TimeSpan.FromMilliseconds(100);

            // Send initial progress event
            await SendStandaloneProgressAsync(operationId, containerName, container.Id, "pull", serviceProgress, null);

            // 1. Pull new image with streaming output
            void OnPullOutput(string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                _logger.LogDebug("Pull output: {Line}", line.Trim());

                bool changed = _progressParser.ParseLine(line, serviceProgress);

                if (changed || DateTime.UtcNow - lastProgressSent > minProgressInterval)
                {
                    lastProgressSent = DateTime.UtcNow;
                    _ = SendStandaloneProgressAsync(operationId, containerName, container.Id, "pull", serviceProgress, line);
                }
            }

            (int pullExitCode, string pullOutput, string pullError) = await _dockerExecutor.ExecuteWithStreamingAsync(
                "docker",
                $"pull {container.Image}",
                OnPullOutput,
                OnPullOutput,
                ct);

            if (pullExitCode != 0)
            {
                serviceProgress[containerName] = serviceProgress[containerName] with
                {
                    Status = "error",
                    Message = "Pull failed"
                };
                await SendStandaloneProgressAsync(operationId, containerName, container.Id, "pull", serviceProgress, pullError);
                return new UpdateTriggerResponse(false, $"Failed to pull image: {pullError}", operationId);
            }

            // Mark pull as completed
            serviceProgress[containerName] = serviceProgress[containerName] with
            {
                Status = "pulled",
                ProgressPercent = 100
            };
            await SendStandaloneProgressAsync(operationId, containerName, container.Id, "pull", serviceProgress, "Pull completed");

            // 2. Stop and remove old container - switch to recreate phase
            serviceProgress[containerName] = new ServicePullProgress(
                ServiceName: containerName,
                Status: "recreating",
                ProgressPercent: 0,
                Message: "Stopping container..."
            );
            await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, "Stopping container...");

            (int stopExitCode, _, _) = await _dockerExecutor.ExecuteAsync(
                "docker",
                $"stop {container.Id}",
                ct);

            if (stopExitCode != 0 && container.State == "running")
            {
                serviceProgress[containerName] = serviceProgress[containerName] with
                {
                    Status = "error",
                    Message = "Failed to stop container"
                };
                await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, "Failed to stop container");
                return new UpdateTriggerResponse(false, "Failed to stop container", operationId);
            }

            serviceProgress[containerName] = serviceProgress[containerName] with
            {
                ProgressPercent = 30,
                Message = "Removing old container..."
            };
            await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, "Removing old container...");

            (int rmExitCode, _, string rmError) = await _dockerExecutor.ExecuteAsync(
                "docker",
                $"rm {container.Id}",
                ct);

            if (rmExitCode != 0)
            {
                serviceProgress[containerName] = serviceProgress[containerName] with
                {
                    Status = "error",
                    Message = $"Failed to remove container: {rmError}"
                };
                await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, rmError);
                return new UpdateTriggerResponse(false, $"Failed to remove container: {rmError}", operationId);
            }

            // 3. Recreate container with same config using docker run
            serviceProgress[containerName] = serviceProgress[containerName] with
            {
                ProgressPercent = 60,
                Message = "Creating new container..."
            };
            await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, "Creating new container...");

            string runArgs = BuildDockerRunArgs(container);
            _logger.LogDebug("Recreating container with args: docker run {Args}", runArgs);

            (int runExitCode, string runOutput, string runError) = await _dockerExecutor.ExecuteAsync(
                "docker",
                $"run {runArgs}",
                ct);

            if (runExitCode != 0)
            {
                _logger.LogError("Failed to recreate container {ContainerName}: {Error}", containerName, runError);
                serviceProgress[containerName] = serviceProgress[containerName] with
                {
                    Status = "error",
                    Message = $"Failed to recreate container: {runError}"
                };
                await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, runError);
                return new UpdateTriggerResponse(false, $"Failed to recreate container: {runError}", operationId);
            }

            // Mark as completed
            serviceProgress[containerName] = serviceProgress[containerName] with
            {
                Status = "completed",
                ProgressPercent = 100,
                Message = "Update completed"
            };
            await SendStandaloneProgressAsync(operationId, containerName, container.Id, "recreate", serviceProgress, "Update completed");

            // Audit log
            await _auditService.LogActionAsync(
                userId,
                "container.standalone_update",
                ipAddress,
                $"Updated standalone container {containerName} ({container.Image})",
                resourceType: "container",
                resourceId: container.Id
            );

            _logger.LogInformation("Successfully updated standalone container {ContainerName}", containerName);

            return new UpdateTriggerResponse(true, $"Successfully updated container {containerName}", operationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating standalone container {ContainerName}", container.Name);
            return new UpdateTriggerResponse(false, $"Error updating container: {ex.Message}", operationId);
        }
    }

    private async Task SendStandaloneProgressAsync(
        string operationId,
        string containerName,
        string containerId,
        string phase,
        Dictionary<string, ServicePullProgress> serviceProgress,
        string? currentLog)
    {
        try
        {
            int overallProgress = _progressParser.CalculateOverallProgress(serviceProgress);

            // During recreate phase, add 50% base since pull is complete
            if (phase == "recreate")
            {
                overallProgress = 50 + (overallProgress / 2);
            }
            else
            {
                // Pull phase is first 50%
                overallProgress = overallProgress / 2;
            }

            UpdateProgressEvent progressEvent = new UpdateProgressEvent(
                OperationId: operationId,
                ProjectName: containerName,
                Phase: phase,
                OverallProgress: overallProgress,
                Services: serviceProgress.Values.ToList(),
                CurrentLog: currentLog,
                ContainerId: containerId
            );

            await _operationService.SendPullProgressAsync(progressEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send standalone progress update for operation {OperationId}", operationId);
        }
    }

    /// <summary>
    /// Builds docker run arguments to recreate a container with the same configuration.
    /// </summary>
    private static string BuildDockerRunArgs(ContainerDetailsDto container)
    {
        List<string> args = new() { "-d" };

        // Container name
        string containerName = container.Name.TrimStart('/');
        args.Add($"--name \"{containerName}\"");

        // Environment variables
        if (container.Env != null)
        {
            foreach ((string key, string value) in container.Env)
            {
                args.Add($"-e \"{key}={value}\"");
            }
        }

        // Ports
        if (container.PortDetails != null)
        {
            foreach ((string containerPort, string hostBinding) in container.PortDetails)
            {
                // containerPort is like "80/tcp", hostBinding is like "0.0.0.0:7070"
                args.Add($"-p {hostBinding}:{containerPort}");
            }
        }

        // Volumes/Mounts
        if (container.Mounts != null)
        {
            foreach (MountDto mount in container.Mounts)
            {
                string roFlag = mount.ReadOnly ? ":ro" : "";
                if (mount.Type == "bind")
                {
                    args.Add($"-v \"{mount.Source}:{mount.Destination}{roFlag}\"");
                }
                else if (mount.Type == "volume")
                {
                    args.Add($"-v \"{mount.Source}:{mount.Destination}{roFlag}\"");
                }
            }
        }

        // Networks
        if (container.Networks != null && container.Networks.Count > 0)
        {
            // docker run only supports one --network flag, use the first one
            args.Add($"--network \"{container.Networks[0]}\"");
        }

        // Labels (preserve non-compose labels)
        if (container.Labels != null)
        {
            foreach ((string key, string value) in container.Labels)
            {
                // Skip compose-specific labels
                if (key.StartsWith("com.docker.compose."))
                    continue;

                args.Add($"--label \"{key}={value}\"");
            }
        }

        // Image
        args.Add(container.Image);

        return string.Join(" ", args);
    }

    public async Task<ContainerUpdatesCheckedEvent> CheckAllContainerUpdatesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Checking updates for all containers");

        List<ContainerDto> containers = await _dockerService.ListContainersAsync(true);
        List<ContainerUpdateSummary> summaries = new();
        int containersWithUpdates = 0;
        int resolvedFromCache = 0;

        foreach (ContainerDto container in containers)
        {
            if (ct.IsCancellationRequested) break;

            string? projectName = container.Labels?.GetValueOrDefault("com.docker.compose.project");
            string? serviceName = container.Labels?.GetValueOrDefault("com.docker.compose.service");
            bool isComposeManaged = !string.IsNullOrEmpty(projectName);

            try
            {
                bool updateAvailable = false;

                // For compose-managed containers, try to resolve from project update cache
                if (isComposeManaged && !string.IsNullOrEmpty(serviceName))
                {
                    ProjectUpdateCheckResponse? cached = _updateCacheService.GetCachedCheck(projectName!);
                    if (cached != null)
                    {
                        ImageUpdateStatus? serviceStatus = cached.Images
                            .FirstOrDefault(i => i.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

                        if (serviceStatus != null)
                        {
                            var cachedResult = new ContainerUpdateCheckResponse(
                                ContainerId: container.Id,
                                ContainerName: container.Name,
                                Image: container.Image,
                                UpdateAvailable: serviceStatus.UpdateAvailable,
                                IsComposeManaged: true,
                                ProjectName: projectName,
                                LocalDigest: serviceStatus.LocalDigest,
                                RemoteDigest: serviceStatus.RemoteDigest,
                                RequiredPull: false,
                                Error: serviceStatus.Error
                            );
                            _containerUpdateCacheService.SetCachedCheck(container.Id, cachedResult);
                            updateAvailable = serviceStatus.UpdateAvailable;
                            resolvedFromCache++;

                            summaries.Add(new ContainerUpdateSummary(
                                ContainerId: container.Id,
                                ContainerName: container.Name,
                                Image: container.Image,
                                UpdateAvailable: updateAvailable,
                                IsComposeManaged: true,
                                ProjectName: projectName
                            ));

                            if (updateAvailable) containersWithUpdates++;
                            continue;
                        }
                    }
                }

                // Standalone container or compose container without cached project data: do real check
                ContainerUpdateCheckResponse result = await CheckContainerUpdateAsync(container.Id, ct);
                updateAvailable = result.UpdateAvailable;

                summaries.Add(new ContainerUpdateSummary(
                    ContainerId: container.Id,
                    ContainerName: container.Name,
                    Image: container.Image,
                    UpdateAvailable: updateAvailable,
                    IsComposeManaged: isComposeManaged,
                    ProjectName: projectName
                ));

                if (updateAvailable) containersWithUpdates++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking update for container {ContainerId}", container.Id);
                summaries.Add(new ContainerUpdateSummary(
                    ContainerId: container.Id,
                    ContainerName: container.Name,
                    Image: container.Image,
                    UpdateAvailable: false,
                    IsComposeManaged: isComposeManaged,
                    ProjectName: projectName
                ));
            }
        }

        ContainerUpdatesCheckedEvent result2 = new(
            Containers: summaries,
            ContainersChecked: summaries.Count,
            ContainersWithUpdates: containersWithUpdates,
            CheckedAt: DateTime.UtcNow
        );

        // Broadcast via SSE
        await _sseManager.BroadcastAsync("ContainerUpdatesChecked", result2);

        _logger.LogInformation(
            "Container update check complete: {Checked} containers checked, {WithUpdates} with updates ({FromCache} resolved from project cache)",
            summaries.Count,
            containersWithUpdates,
            resolvedFromCache);

        return result2;
    }

    public List<ContainerUpdateSummary> GetCachedContainerUpdateStatus()
    {
        return _containerUpdateCacheService.GetAllCachedSummaries();
    }
}
