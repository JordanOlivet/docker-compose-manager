using docker_compose_manager_back.Configuration;
using docker_compose_manager_back.DTOs;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for checking and performing compose project updates.
/// </summary>
public interface IComposeUpdateService
{
    /// <summary>
    /// Checks for available updates for a project's images.
    /// </summary>
    Task<ProjectUpdateCheckResponse> CheckProjectUpdatesAsync(
        string projectName,
        CancellationToken ct = default);

    /// <summary>
    /// Updates selected services in a project by pulling new images and recreating containers.
    /// </summary>
    Task<UpdateTriggerResponse> UpdateProjectAsync(
        string projectName,
        List<string>? services,
        bool updateAll,
        bool restartFullProject,
        int userId,
        string ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Gets global update status for all cached projects.
    /// </summary>
    List<ProjectUpdateSummary> GetGlobalUpdateStatus();

    /// <summary>
    /// Updates all projects that have available updates.
    /// </summary>
    Task<UpdateAllResponse> UpdateAllProjectsAsync(
        int userId,
        string ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Clears the update check cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Checks for updates across all projects that have compose files.
    /// </summary>
    Task<CheckAllUpdatesResponse> CheckAllProjectsUpdatesAsync(
        int userId,
        CancellationToken ct = default);
}

public class ComposeUpdateService : IComposeUpdateService
{
    private readonly IComposeDiscoveryService _discoveryService;
    private readonly IComposeOperationService _composeOperationService;
    private readonly IImageDigestService _imageDigestService;
    private readonly IImageUpdateCacheService _cacheService;
    private readonly IComposeFileCacheService _fileCacheService;
    private readonly IProjectMatchingService _projectMatchingService;
    private readonly IAuditService _auditService;
    private readonly DockerCommandExecutorService _dockerExecutor;
    private readonly DockerPullProgressParser _progressParser;
    private readonly OperationService _operationService;
    private readonly SseConnectionManagerService _sseManager;
    private readonly ILogger<ComposeUpdateService> _logger;
    private readonly UpdateCheckOptions _options;

    private readonly SemaphoreSlim _checkLock = new(1, 1);

    public ComposeUpdateService(
        IComposeDiscoveryService discoveryService,
        IComposeOperationService operationService,
        IImageDigestService imageDigestService,
        IImageUpdateCacheService cacheService,
        IComposeFileCacheService fileCacheService,
        IProjectMatchingService projectMatchingService,
        IAuditService auditService,
        DockerCommandExecutorService dockerExecutor,
        DockerPullProgressParser progressParser,
        OperationService operationServiceDb,
        SseConnectionManagerService sseManager,
        IOptions<UpdateCheckOptions> options,
        ILogger<ComposeUpdateService> logger)
    {
        _discoveryService = discoveryService;
        _composeOperationService = operationService;
        _imageDigestService = imageDigestService;
        _cacheService = cacheService;
        _fileCacheService = fileCacheService;
        _projectMatchingService = projectMatchingService;
        _auditService = auditService;
        _dockerExecutor = dockerExecutor;
        _progressParser = progressParser;
        _operationService = operationServiceDb;
        _sseManager = sseManager;
        _options = options.Value;
        _logger = logger;
    }

    public Task<ProjectUpdateCheckResponse> CheckProjectUpdatesAsync(
        string projectName,
        CancellationToken ct = default)
    {
        return CheckProjectUpdatesInternalAsync(projectName, null, ct);
    }

    private async Task<ProjectUpdateCheckResponse> CheckProjectUpdatesInternalAsync(
        string projectName,
        string? knownComposeFilePath,
        CancellationToken ct = default)
    {
        // Check cache first
        ProjectUpdateCheckResponse? cached = _cacheService.GetCachedCheck(projectName);
        if (cached != null)
        {
            return cached;
        }

        await _checkLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            cached = _cacheService.GetCachedCheck(projectName);
            if (cached != null)
            {
                return cached;
            }

            _logger.LogDebug("Checking updates for project {ProjectName}", projectName);

            // Use pre-resolved path if available, otherwise look it up
            string? composeFilePath = knownComposeFilePath ?? await FindComposeFilePathAsync(projectName);

            if (composeFilePath == null)
            {
                _logger.LogWarning("No compose file found for project {ProjectName}", projectName);
                return new ProjectUpdateCheckResponse(
                    ProjectName: projectName,
                    Images: new List<ImageUpdateStatus>(),
                    HasUpdates: false,
                    LastChecked: DateTime.UtcNow
                );
            }

            _logger.LogDebug("Found compose file for project {ProjectName}: {FilePath}", projectName, composeFilePath);

            // Parse compose file to get services and images
            Dictionary<string, ServiceImageInfo> serviceImages = await ParseServiceImagesAsync(composeFilePath, ct);

            // Check each image for updates (with concurrency limit)
            SemaphoreSlim semaphore = new SemaphoreSlim(_options.MaxConcurrentChecks);
            List<Task<ImageUpdateStatus>> tasks = new List<Task<ImageUpdateStatus>>();

            foreach ((string? serviceName, ServiceImageInfo? imageInfo) in serviceImages)
            {
                if (string.IsNullOrEmpty(imageInfo.Image))
                {
                    // Service uses build, not image
                    continue;
                }

                // Check if image is excluded
                if (IsImageExcluded(imageInfo.Image))
                {
                    _logger.LogDebug("Image {Image} is excluded from update checks", imageInfo.Image);
                    continue;
                }

                tasks.Add(CheckImageWithSemaphoreAsync(
                    semaphore,
                    imageInfo.Image,
                    serviceName,
                    imageInfo.UpdatePolicy,
                    ct));
            }

            ImageUpdateStatus[] results = await Task.WhenAll(tasks);

            ProjectUpdateCheckResponse response = new ProjectUpdateCheckResponse(
                ProjectName: projectName,
                Images: results.ToList(),
                HasUpdates: results.Any(r => r.UpdateAvailable && r.UpdatePolicy != "disabled"),
                LastChecked: DateTime.UtcNow
            );

            // Cache the result
            _cacheService.SetCachedCheck(projectName, response);

            _logger.LogDebug(
                "Update check complete for {ProjectName}: {UpdateCount} updates available out of {TotalCount} images",
                projectName,
                results.Count(r => r.UpdateAvailable),
                results.Length);

            return response;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private async Task<ImageUpdateStatus> CheckImageWithSemaphoreAsync(
        SemaphoreSlim semaphore,
        string image,
        string serviceName,
        string? updatePolicy,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            ImageUpdateStatus status = await _imageDigestService.CheckImageUpdateAsync(image, serviceName, ct);

            // Apply update policy from compose file
            return status with { UpdatePolicy = updatePolicy };
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<UpdateTriggerResponse> UpdateProjectAsync(
        string projectName,
        List<string>? services,
        bool updateAll,
        bool restartFullProject,
        int userId,
        string ipAddress,
        CancellationToken ct = default)
    {
        string operationId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogDebug(
                "Updating project {ProjectName} - Services: {Services}, UpdateAll: {UpdateAll}",
                projectName,
                services != null ? string.Join(", ", services) : "all",
                updateAll);

            // Use ProjectMatchingService to find the compose file path
            string? composeFilePath = await FindComposeFilePathAsync(projectName);

            if (composeFilePath == null)
            {
                return new UpdateTriggerResponse(
                    Success: false,
                    Message: $"No compose file found for project {projectName}",
                    OperationId: null
                );
            }

            _logger.LogDebug("Found compose file for project {ProjectName}: {FilePath}", projectName, composeFilePath);

            // Determine which services to update
            List<string> servicesToUpdate;
            if (updateAll || services == null || services.Count == 0)
            {
                // Update all services with available updates
                ProjectUpdateCheckResponse updateCheck = await CheckProjectUpdatesAsync(projectName, ct);
                servicesToUpdate = updateCheck.Images
                    .Where(i => i.UpdateAvailable && i.UpdatePolicy != "disabled")
                    .Select(i => i.ServiceName)
                    .ToList();
            }
            else
            {
                servicesToUpdate = services;
            }

            if (servicesToUpdate.Count == 0)
            {
                return new UpdateTriggerResponse(
                    Success: true,
                    Message: "No services need updating",
                    OperationId: null
                );
            }

            string servicesArg = string.Join(" ", servicesToUpdate);

            // Reset parser state and initialize progress tracking for all services
            _progressParser.Reset();
            Dictionary<string, ServicePullProgress> serviceProgress = _progressParser.InitializeProgress(servicesToUpdate);
            string? lastLogLine = null;
            DateTime lastProgressSent = DateTime.MinValue;
            TimeSpan minProgressInterval = TimeSpan.FromMilliseconds(100); // Throttle to max 10 updates per second

            // Send initial progress event
            await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, null);

            // Pull new images with streaming output
            _logger.LogDebug("Pulling images for services: {Services}", servicesArg);

            void OnPullOutput(string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                lastLogLine = line;
                _logger.LogDebug("Pull output: {Line}", line.Trim());

                bool changed = _progressParser.ParseLine(line, serviceProgress);

                // Send progress update if state changed or enough time has passed
                if (changed || DateTime.UtcNow - lastProgressSent > minProgressInterval)
                {
                    lastProgressSent = DateTime.UtcNow;
                    int progress = _progressParser.CalculateOverallProgress(serviceProgress);
                    _logger.LogDebug("Pull progress update - Changed: {Changed}, Overall: {Progress}%, Services: {Services}",
                        changed, progress,
                        string.Join(", ", serviceProgress.Select(s => $"{s.Key}:{s.Value.Status}({s.Value.ProgressPercent}%)")));

                    // Fire and forget - don't block the output processing
                    _ = SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, line);
                }
            }

            (int pullExitCode, string pullOutput, string pullError) = await _dockerExecutor.ExecuteWithStreamingAsync(
                "docker",
                $"compose -f \"{composeFilePath}\" pull {servicesArg}",
                OnPullOutput,
                OnPullOutput, // Also capture stderr as it may contain progress info
                ct);

            if (pullExitCode != 0)
            {
                _logger.LogError("Pull failed for {ProjectName}: {Error}", projectName, pullError);

                // Mark all services as error
                foreach (string serviceName in servicesToUpdate)
                {
                    if (serviceProgress.ContainsKey(serviceName) && serviceProgress[serviceName].Status != "pulled")
                    {
                        serviceProgress[serviceName] = serviceProgress[serviceName] with
                        {
                            Status = "error",
                            Message = "Pull failed"
                        };
                    }
                }
                await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, pullError);

                return new UpdateTriggerResponse(
                    Success: false,
                    Message: $"Failed to pull images: {pullError}",
                    OperationId: operationId
                );
            }

            // Mark all services as pulled after successful pull
            foreach (string serviceName in servicesToUpdate)
            {
                serviceProgress[serviceName] = serviceProgress[serviceName] with
                {
                    Status = "pulled",
                    ProgressPercent = 100
                };
            }
            await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, "Pull completed");

            // Reset progress for recreate phase
            foreach (string serviceName in servicesToUpdate)
            {
                serviceProgress[serviceName] = new ServicePullProgress(
                    ServiceName: serviceName,
                    Status: "recreating",
                    ProgressPercent: 0,
                    Message: null
                );
            }
            await SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, "Recreating containers...");

            // Recreate containers with new images
            string recreateArgs = restartFullProject
                ? $"compose -f \"{composeFilePath}\" up -d --force-recreate"
                : $"compose -f \"{composeFilePath}\" up -d --force-recreate {servicesArg}";
            _logger.LogDebug("Recreating containers - RestartFullProject: {RestartFullProject}, Command args: {Args}", restartFullProject, recreateArgs);

            void OnRecreateOutput(string line)
            {
                lastLogLine = line;
                // For recreate, we just stream the logs without detailed parsing
                if (DateTime.UtcNow - lastProgressSent > minProgressInterval)
                {
                    lastProgressSent = DateTime.UtcNow;
                    _ = SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, line);
                }
            }

            (int upExitCode, string upOutput, string upError) = await _dockerExecutor.ExecuteWithStreamingAsync(
                "docker",
                recreateArgs,
                OnRecreateOutput,
                OnRecreateOutput,
                ct);

            if (upExitCode != 0)
            {
                _logger.LogError("Up failed for {ProjectName}: {Error}", projectName, upError);

                // Mark all services as error
                foreach (string serviceName in servicesToUpdate)
                {
                    serviceProgress[serviceName] = serviceProgress[serviceName] with
                    {
                        Status = "error",
                        Message = "Recreate failed"
                    };
                }
                await SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, upError);

                return new UpdateTriggerResponse(
                    Success: false,
                    Message: $"Failed to recreate containers: {upError}",
                    OperationId: operationId
                );
            }

            // Mark all services as completed
            foreach (string serviceName in servicesToUpdate)
            {
                serviceProgress[serviceName] = serviceProgress[serviceName] with
                {
                    Status = "completed",
                    ProgressPercent = 100
                };
            }
            await SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, "Update completed");

            // Invalidate cache for this project
            _cacheService.InvalidateProject(projectName);

            // Audit log
            string auditDetail = restartFullProject
                ? $"Updated {servicesToUpdate.Count} services in project {projectName}: {servicesArg} (full project restart)"
                : $"Updated {servicesToUpdate.Count} services in project {projectName}: {servicesArg}";
            await _auditService.LogActionAsync(
                userId,
                "compose.project_update",
                ipAddress,
                auditDetail,
                resourceType: "compose_project",
                resourceId: projectName
            );

            _logger.LogDebug(
                "Successfully updated {Count} services in project {ProjectName}",
                servicesToUpdate.Count,
                projectName);

            return new UpdateTriggerResponse(
                Success: true,
                Message: $"Successfully updated {servicesToUpdate.Count} services",
                OperationId: operationId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectName}", projectName);
            return new UpdateTriggerResponse(
                Success: false,
                Message: $"Error updating project: {ex.Message}",
                OperationId: operationId
            );
        }
    }

    private async Task SendProgressUpdateAsync(
        string operationId,
        string projectName,
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
                ProjectName: projectName,
                Phase: phase,
                OverallProgress: overallProgress,
                Services: serviceProgress.Values.ToList(),
                CurrentLog: currentLog
            );

            await _operationService.SendPullProgressAsync(progressEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send progress update for operation {OperationId}", operationId);
            // Don't fail the operation if SignalR notification fails
        }
    }

    public List<ProjectUpdateSummary> GetGlobalUpdateStatus()
    {
        return _cacheService.GetAllCachedSummaries();
    }

    public async Task<UpdateAllResponse> UpdateAllProjectsAsync(
        int userId,
        string ipAddress,
        CancellationToken ct = default)
    {
        List<ProjectUpdateSummary> summaries = _cacheService.GetAllCachedSummaries();
        List<string> projectsWithUpdates = summaries
            .Where(s => s.ServicesWithUpdates > 0)
            .Select(s => s.ProjectName)
            .ToList();

        if (projectsWithUpdates.Count == 0)
        {
            return new UpdateAllResponse(
                OperationId: Guid.NewGuid().ToString(),
                ProjectsToUpdate: new List<string>(),
                Status: "No projects with available updates"
            );
        }

        string operationId = Guid.NewGuid().ToString();

        // Start updates in background
        _ = Task.Run(async () =>
        {
            foreach (string projectName in projectsWithUpdates)
            {
                try
                {
                    await UpdateProjectAsync(projectName, null, true, true, userId, ipAddress, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating project {ProjectName} during update-all", projectName);
                }
            }
        }, ct);

        await _auditService.LogActionAsync(
            userId,
            "compose.update_all",
            ipAddress,
            $"Started update-all for {projectsWithUpdates.Count} projects",
            resourceType: "System",
            resourceId: "UpdateAll"
        );

        return new UpdateAllResponse(
            OperationId: operationId,
            ProjectsToUpdate: projectsWithUpdates,
            Status: "Update started"
        );
    }

    public void ClearCache()
    {
        _cacheService.InvalidateAll();
        _logger.LogDebug("Update check cache cleared");
    }

    public async Task<CheckAllUpdatesResponse> CheckAllProjectsUpdatesAsync(
        int userId,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Checking updates for all projects (user: {UserId})", userId);

        // Get all projects with compose files
        List<ComposeProjectDto> allProjects = await _projectMatchingService.GetUnifiedProjectListAsync(userId);
        List<ComposeProjectDto> projectsWithFiles = allProjects
            .Where(p => p.HasComposeFile && !string.IsNullOrEmpty(p.ComposeFilePath))
            .ToList();

        _logger.LogDebug("Found {Count} projects with compose files to check", projectsWithFiles.Count);

        List<ProjectUpdateSummary> summaries = new List<ProjectUpdateSummary>();
        int totalServicesWithUpdates = 0;

        // Check each project sequentially to avoid rate limiting
        foreach (ComposeProjectDto? project in projectsWithFiles)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                ProjectUpdateCheckResponse checkResult = await CheckProjectUpdatesInternalAsync(project.Name, project.ComposeFilePath, ct);

                int servicesWithUpdates = checkResult.Images
                    .Count(i => i.UpdateAvailable && i.UpdatePolicy != "disabled");

                summaries.Add(new ProjectUpdateSummary(
                    ProjectName: project.Name,
                    ServicesWithUpdates: servicesWithUpdates,
                    LastChecked: checkResult.LastChecked
                ));

                totalServicesWithUpdates += servicesWithUpdates;

                _logger.LogDebug(
                    "Project {ProjectName}: {UpdateCount} services with updates",
                    project.Name,
                    servicesWithUpdates);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking updates for project {ProjectName}", project.Name);
                // Add project with 0 updates on error so it still appears in the list
                summaries.Add(new ProjectUpdateSummary(
                    ProjectName: project.Name,
                    ServicesWithUpdates: 0,
                    LastChecked: null
                ));
            }
        }

        int projectsWithUpdates = summaries.Count(s => s.ServicesWithUpdates > 0);

        _logger.LogDebug(
            "Bulk update check complete: {ProjectsChecked} projects checked, {ProjectsWithUpdates} with updates, {TotalServices} total services",
            summaries.Count,
            projectsWithUpdates,
            totalServicesWithUpdates);

        CheckAllUpdatesResponse response = new CheckAllUpdatesResponse(
            Projects: summaries,
            ProjectsChecked: summaries.Count,
            ProjectsWithUpdates: projectsWithUpdates,
            TotalServicesWithUpdates: totalServicesWithUpdates,
            CheckedAt: DateTime.UtcNow
        );

        // Broadcast SSE event for manual check
        try
        {
            ProjectUpdatesCheckedEvent sseEvent = new(
                Projects: response.Projects,
                ProjectsChecked: response.ProjectsChecked,
                ProjectsWithUpdates: response.ProjectsWithUpdates,
                TotalServicesWithUpdates: response.TotalServicesWithUpdates,
                CheckedAt: response.CheckedAt,
                Trigger: "manual"
            );
            await _sseManager.BroadcastAsync("ProjectUpdatesChecked", sseEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ProjectUpdatesChecked SSE event");
        }

        return response;
    }

    private async Task<Dictionary<string, ServiceImageInfo>> ParseServiceImagesAsync(
        string composeFilePath,
        CancellationToken ct)
    {
        Dictionary<string, ServiceImageInfo> result = new Dictionary<string, ServiceImageInfo>();

        try
        {
            string content = await File.ReadAllTextAsync(composeFilePath, ct);

            IDeserializer deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            Dictionary<string, object> composeData = deserializer.Deserialize<Dictionary<string, object>>(content);

            if (composeData == null ||
                !composeData.TryGetValue("services", out object? servicesObj) ||
                servicesObj is not Dictionary<object, object> services)
            {
                return result;
            }

            foreach ((object? serviceKey, object? serviceValue) in services)
            {
                string serviceName = serviceKey.ToString() ?? "";
                if (serviceValue is not Dictionary<object, object> serviceData)
                    continue;

                string? image = null;
                string? updatePolicy = null;

                if (serviceData.TryGetValue("image", out object? imageObj))
                {
                    image = imageObj?.ToString();
                }

                // Check for x-update-policy extension
                if (serviceData.TryGetValue("x-update-policy", out object? policyObj))
                {
                    updatePolicy = policyObj?.ToString()?.ToLowerInvariant();
                }

                // Also check at root level for project-wide policy
                if (updatePolicy == null && composeData.TryGetValue("x-update-policy", out object? rootPolicyObj))
                {
                    updatePolicy = rootPolicyObj?.ToString()?.ToLowerInvariant();
                }

                result[serviceName] = new ServiceImageInfo(image, updatePolicy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing compose file {Path}", composeFilePath);
        }

        return result;
    }

    private bool IsImageExcluded(string image)
    {
        foreach (string pattern in _options.ExcludedImages)
        {
            if (MatchesPattern(image, pattern))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        // Simple wildcard matching
        if (pattern.Contains('*'))
        {
            string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(
                value,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds the compose file path for a project using the ProjectMatchingService.
    /// This reuses the same sophisticated matching logic used for the projects list,
    /// handling cases where Docker's project name differs from the compose file's project name.
    /// </summary>
    /// <param name="projectName">The project name as reported by Docker</param>
    /// <returns>The compose file path if found, null otherwise</returns>
    private async Task<string?> FindComposeFilePathAsync(string projectName)
    {
        try
        {
            // Use the ProjectMatchingService to get the unified project list
            // This uses the same matching strategies (by name, by path, by filename+directory)
            // We use userId 1 (default admin) since this is only called from admin endpoints
            const int systemAdminUserId = 1;
            List<ComposeProjectDto> projects = await _projectMatchingService.GetUnifiedProjectListAsync(systemAdminUserId);

            // Find the project by name (case-insensitive)
            ComposeProjectDto? matchedProject = projects.FirstOrDefault(p =>
                p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            if (matchedProject != null && matchedProject.HasComposeFile)
            {
                _logger.LogDebug(
                    "Found compose file via ProjectMatchingService for project {ProjectName}: {FilePath}",
                    projectName,
                    matchedProject.ComposeFilePath);
                return matchedProject.ComposeFilePath;
            }

            _logger.LogDebug(
                "No compose file found via ProjectMatchingService for project {ProjectName}",
                projectName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding compose file path for project {ProjectName}", projectName);
            return null;
        }
    }

    private record ServiceImageInfo(string? Image, string? UpdatePolicy);
}
