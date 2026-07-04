using System.Text;
using Lighthouse.Configuration;
using Lighthouse.DTOs;
using Lighthouse.Services.Registry;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;

namespace Lighthouse.Services;

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
        bool forceRefresh = false,
        CancellationToken ct = default);

    /// <summary>
    /// Updates selected services in a project by pulling new images and recreating containers.
    /// </summary>
    Task<UpdateTriggerResponse> UpdateProjectAsync(
        string projectName,
        List<string>? services,
        bool updateAll,
        bool restartFullProject,
        bool restartAfterUpdate,
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
        bool forceRefresh = false,
        CancellationToken ct = default);
}

public class ComposeUpdateService : IComposeUpdateService
{
    private readonly IComposeUpdateChecker _checker;
    private readonly IImageUpdateCacheService _cacheService;
    private readonly IAuditService _auditService;
    private readonly DockerCommandExecutorService _dockerExecutor;
    private readonly IComposeEnvFileResolver _envFileResolver;
    private readonly DockerPullProgressParser _progressParser;
    private readonly IOperationService _operationService;
    private readonly IRegistryRateLimitGate _rateLimitGate;
    private readonly ILogger<ComposeUpdateService> _logger;
    private readonly UpdateCheckOptions _options;

    public ComposeUpdateService(
        IComposeUpdateChecker checker,
        IImageUpdateCacheService cacheService,
        IAuditService auditService,
        DockerCommandExecutorService dockerExecutor,
        IComposeEnvFileResolver envFileResolver,
        DockerPullProgressParser progressParser,
        IOperationService operationServiceDb,
        IRegistryRateLimitGate rateLimitGate,
        IOptions<UpdateCheckOptions> options,
        ILogger<ComposeUpdateService> logger)
    {
        _checker = checker;
        _cacheService = cacheService;
        _auditService = auditService;
        _dockerExecutor = dockerExecutor;
        _envFileResolver = envFileResolver;
        _progressParser = progressParser;
        _operationService = operationServiceDb;
        _rateLimitGate = rateLimitGate;
        _options = options.Value;
        _logger = logger;
    }

    // Update check / status concern is delegated to the ComposeUpdateChecker collaborator.

    public Task<ProjectUpdateCheckResponse> CheckProjectUpdatesAsync(
        string projectName,
        bool forceRefresh = false,
        CancellationToken ct = default)
        => _checker.CheckProjectUpdatesAsync(projectName, forceRefresh, ct);

    public List<ProjectUpdateSummary> GetGlobalUpdateStatus()
        => _checker.GetGlobalUpdateStatus();

    public void ClearCache()
        => _checker.ClearCache();

    public Task<CheckAllUpdatesResponse> CheckAllProjectsUpdatesAsync(
        int userId,
        bool forceRefresh = false,
        CancellationToken ct = default)
        => _checker.CheckAllProjectsUpdatesAsync(userId, forceRefresh, ct);

    public async Task<UpdateTriggerResponse> UpdateProjectAsync(
        string projectName,
        List<string>? services,
        bool updateAll,
        bool restartFullProject,
        bool restartAfterUpdate,
        int userId,
        string ipAddress,
        CancellationToken ct = default)
    {
        string operationId = Guid.NewGuid().ToString();
        bool operationCreated = false;
        var filteredLogs = new StringBuilder();

        try
        {
            _logger.LogDebug(
                "Updating project {ProjectName} - Services: {Services}, UpdateAll: {UpdateAll}",
                projectName,
                services != null ? string.Join(", ", services) : "all",
                updateAll);

            // Use the checker to resolve the compose file path
            string? composeFilePath = await _checker.FindComposeFilePathAsync(projectName);

            if (composeFilePath == null)
            {
                return new UpdateTriggerResponse(
                    Success: false,
                    Message: $"No compose file found for project {projectName}",
                    OperationId: null
                );
            }

            _logger.LogDebug("Found compose file for project {ProjectName}: {FilePath}", projectName, composeFilePath);

            // Run compose from the compose file's directory so docker auto-loads the adjacent .env
            // (compose discovers .env from the working directory, not the -f file's directory), and
            // re-apply any configured global env file since docker does not persist the --env-file
            // originally used to start the project.
            string composeDirectory = Path.GetDirectoryName(composeFilePath) ?? "/";
            string envFileArgs = await _envFileResolver.BuildEnvFileArgsAsync(composeDirectory, ct);

            // Determine which services to update
            List<string> servicesToUpdate;
            if (updateAll || services == null || services.Count == 0)
            {
                // Update all services with available updates
                ProjectUpdateCheckResponse updateCheck = await CheckProjectUpdatesAsync(projectName, ct: ct);
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

            // Create operation for action log tracking
            await _operationService.CreateOperationAsync(
                Models.OperationType.ComposeUpdate, userId,
                projectPath: composeFilePath, projectName: projectName,
                operationId: operationId);
            operationCreated = true;
            await _operationService.UpdateOperationStatusAsync(operationId, Models.OperationStatus.Running);
            filteredLogs.AppendLine("Pulling images...");

            // Reset parser state and initialize progress tracking for all services
            _progressParser.Reset();
            Dictionary<string, ServicePullProgress> serviceProgress = _progressParser.InitializeProgress(servicesToUpdate);
            string? lastLogLine = null;
            DateTime lastProgressSent = DateTime.MinValue;
            TimeSpan minProgressInterval = TimeSpan.FromMilliseconds(100); // Throttle to max 10 updates per second

            // Send initial progress event
            await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, null, restartAfterUpdate);

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

                    // Snapshot state before firing to avoid stale data when the task runs
                    // (serviceProgress may be mutated further by subsequent OnPullOutput calls)
                    var progressSnapshot = serviceProgress.ToDictionary(k => k.Key, v => v.Value);
                    _ = SendProgressUpdateAsync(operationId, projectName, "pull", progressSnapshot, line, restartAfterUpdate);
                }
            }

            string pullCommandArgs = $"compose {envFileArgs}-f \"{composeFilePath}\" pull {servicesArg}";

            // Retry a rate-limited pull (HTTP 429 / toomanyrequests). ghcr.io and similar token-bucket
            // limiters report a sub-millisecond Retry-After, so the daemon's single attempt fails even
            // though an immediate retry with a small floor delay usually succeeds. We also honor and
            // trip the shared cooldown gate so concurrent/next work backs off too.
            int pullExitCode = 0;
            string pullOutput = string.Empty;
            string pullError = string.Empty;
            int maxPullAttempts = Math.Max(1, _options.PullRetryAttempts + 1);
            TimeSpan pullMaxDelay = TimeSpan.FromSeconds(_options.PullRetryMaxDelaySeconds);

            for (int attempt = 1; attempt <= maxPullAttempts; attempt++)
            {
                // Honor a cooldown set by an earlier 429 (this pull or a concurrent digest check),
                // bounded so a long cooldown never stalls the update indefinitely.
                if (_rateLimitGate.IsCoolingDown(out TimeSpan cooldown))
                {
                    TimeSpan wait = cooldown < pullMaxDelay ? cooldown : pullMaxDelay;
                    if (wait > TimeSpan.Zero)
                    {
                        _logger.LogInformation(
                            "Registry cooling down ({Cooldown:0}s); waiting {Wait:0}s before pulling {Project}",
                            cooldown.TotalSeconds, wait.TotalSeconds, projectName);
                        await Task.Delay(wait, ct);
                    }
                }

                // Reset streaming progress for a clean retry (layers re-report from scratch).
                if (attempt > 1)
                {
                    _progressParser.Reset();
                    serviceProgress = _progressParser.InitializeProgress(servicesToUpdate);
                    lastProgressSent = DateTime.MinValue;
                }

                (pullExitCode, pullOutput, pullError) = await _dockerExecutor.ExecuteWithStreamingAsync(
                    "docker",
                    pullCommandArgs,
                    OnPullOutput,
                    OnPullOutput, // Also capture stderr as it may contain progress info
                    ct,
                    workingDirectory: composeDirectory);

                if (pullExitCode == 0)
                {
                    break;
                }

                bool isRateLimited = RegistryPullRetry.IsRateLimitError(pullError)
                    || RegistryPullRetry.IsRateLimitError(pullOutput);
                if (isRateLimited && attempt < maxPullAttempts)
                {
                    // ghcr.io meters per minute and reports a bogus sub-millisecond Retry-After, so
                    // we back off on our own schedule (sized to span a minute) with jitter to avoid
                    // re-bursting in lockstep across projects.
                    TimeSpan backoff = RegistryPullRetry.ApplyJitter(
                        RegistryPullRetry.ComputeBackoff(
                            attempt,
                            TimeSpan.FromSeconds(_options.PullRetryBaseDelaySeconds),
                            pullMaxDelay),
                        _options.PullRetryJitterFactor);
                    _rateLimitGate.Trip(backoff);
                    _logger.LogWarning(
                        "Pull for {Project} hit registry rate limit (attempt {Attempt}/{Max}); retrying in {Delay:0}s",
                        projectName, attempt, maxPullAttempts, backoff.TotalSeconds);
                    await Task.Delay(backoff, ct);
                    continue;
                }

                if (isRateLimited)
                {
                    // Out of attempts and still rate limited: trip a real cooldown so the remaining
                    // projects in this cycle back off instead of bursting the registry again.
                    _rateLimitGate.Trip(pullMaxDelay);
                    _logger.LogWarning(
                        "Pull for {Project} still rate limited after {Max} attempts; pausing further pulls for {Delay:0}s",
                        projectName, maxPullAttempts, pullMaxDelay.TotalSeconds);
                }

                // Non-retryable error, or out of attempts.
                break;
            }

            if (pullExitCode != 0)
            {
                _logger.LogError("Pull failed for {ProjectName}: {Error}", projectName, pullError);

                string friendlyPullError = BuildPullErrorMessage(pullError);

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
                await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, pullError, restartAfterUpdate);

                filteredLogs.AppendLine($"Pull failed: {pullError}");
                await _operationService.AppendLogsAsync(operationId, filteredLogs.ToString());
                await _operationService.UpdateOperationStatusAsync(operationId, Models.OperationStatus.Failed, progress: 0, errorMessage: friendlyPullError);

                return new UpdateTriggerResponse(
                    Success: false,
                    Message: friendlyPullError,
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
            await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, "Pull completed", restartAfterUpdate);

            // Skip recreate phase if restartAfterUpdate is false
            if (!restartAfterUpdate)
            {
                // Mark all services as completed (pull only)
                foreach (string serviceName in servicesToUpdate)
                {
                    serviceProgress[serviceName] = serviceProgress[serviceName] with
                    {
                        Status = "completed",
                        ProgressPercent = 100
                    };
                }
                await SendProgressUpdateAsync(operationId, projectName, "pull", serviceProgress, "Images updated (containers not restarted)", restartAfterUpdate);

                filteredLogs.AppendLine($"Pull completed for {servicesToUpdate.Count} services (no restart)");
                await _operationService.AppendLogsAsync(operationId, filteredLogs.ToString());
                await _operationService.UpdateOperationStatusAsync(operationId, Models.OperationStatus.Completed, progress: 100);

                // Invalidate cache for this project
                _cacheService.InvalidateProject(projectName);

                // Audit log
                await _auditService.LogActionAsync(
                    userId,
                    "compose.project_update",
                    ipAddress,
                    $"Pulled images for {servicesToUpdate.Count} services in project {projectName}: {servicesArg} (pull only, no restart)",
                    resourceType: "compose_project",
                    resourceId: projectName
                );

                _logger.LogDebug(
                    "Successfully pulled images for {Count} services in project {ProjectName} (no restart)",
                    servicesToUpdate.Count,
                    projectName);

                return new UpdateTriggerResponse(
                    Success: true,
                    Message: $"Successfully pulled images for {servicesToUpdate.Count} services (containers not restarted)",
                    OperationId: operationId
                );
            }

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
            await SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, "Recreating containers...", restartAfterUpdate);

            // Recreate containers with new images
            string recreateArgs = restartFullProject
                ? $"compose {envFileArgs}-f \"{composeFilePath}\" up -d --force-recreate"
                : $"compose {envFileArgs}-f \"{composeFilePath}\" up -d --force-recreate {servicesArg}";
            _logger.LogDebug("Recreating containers - RestartFullProject: {RestartFullProject}, Command args: {Args}", restartFullProject, recreateArgs);

            var recreateLogs = new StringBuilder();

            void OnRecreateOutput(string line)
            {
                lastLogLine = line;

                string trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    recreateLogs.AppendLine(trimmed);

                // For recreate, we just stream the logs without detailed parsing
                if (DateTime.UtcNow - lastProgressSent > minProgressInterval)
                {
                    lastProgressSent = DateTime.UtcNow;
                    var progressSnapshot = serviceProgress.ToDictionary(k => k.Key, v => v.Value);
                    _ = SendProgressUpdateAsync(operationId, projectName, "recreate", progressSnapshot, line, restartAfterUpdate);
                }
            }

            (int upExitCode, string upOutput, string upError) = await _dockerExecutor.ExecuteWithStreamingAsync(
                "docker",
                recreateArgs,
                OnRecreateOutput,
                OnRecreateOutput,
                ct,
                workingDirectory: composeDirectory);

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
                await SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, upError, restartAfterUpdate);

                filteredLogs.AppendLine($"Pull completed for {servicesToUpdate.Count} services");
                filteredLogs.AppendLine("Recreating containers...");
                filteredLogs.Append(recreateLogs);
                filteredLogs.AppendLine($"Recreate failed: {upError}");
                await _operationService.AppendLogsAsync(operationId, filteredLogs.ToString());
                await _operationService.UpdateOperationStatusAsync(operationId, Models.OperationStatus.Failed, progress: 50, errorMessage: $"Failed to recreate containers: {upError}");

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
            await SendProgressUpdateAsync(operationId, projectName, "recreate", serviceProgress, "Update completed", restartAfterUpdate);

            filteredLogs.AppendLine($"Pull completed for {servicesToUpdate.Count} services");
            filteredLogs.AppendLine("Recreating containers...");
            filteredLogs.Append(recreateLogs);
            filteredLogs.AppendLine("Update completed successfully");
            await _operationService.AppendLogsAsync(operationId, filteredLogs.ToString());
            await _operationService.UpdateOperationStatusAsync(operationId, Models.OperationStatus.Completed, progress: 100);

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

            if (operationCreated)
            {
                try
                {
                    await _operationService.AppendLogsAsync(operationId, filteredLogs.ToString());
                    await _operationService.UpdateOperationStatusAsync(operationId, Models.OperationStatus.Failed, errorMessage: ex.Message);
                }
                catch (Exception opEx)
                {
                    _logger.LogWarning(opEx, "Failed to update operation status for {OperationId}", operationId);
                }
            }

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
        string? currentLog,
        bool restartAfterUpdate = true)
    {
        try
        {
            int overallProgress = _progressParser.CalculateOverallProgress(serviceProgress);

            // Adjust progress based on whether we're doing pull+restart or pull only
            if (restartAfterUpdate)
            {
                // Pull+restart: pull is 0-50%, recreate is 50-100%
                if (phase == "recreate")
                {
                    overallProgress = 50 + (overallProgress / 2);
                }
                else
                {
                    overallProgress = overallProgress / 2;
                }
            }
            // If pull only, progress goes 0-100% for pull phase

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
                    await UpdateProjectAsync(projectName, null, true, true, true, userId, ipAddress, CancellationToken.None);
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

    /// <summary>
    /// Turns a raw `docker compose pull` error into an actionable message. Docker reports
    /// rate-limit and auth failures generically ("toomanyrequests", "unauthorized"); when a
    /// registry credential is invalid or expired the pull silently falls back to anonymous,
    /// so a configured-but-stale login surfaces here as a rate-limit error. The raw error is
    /// kept appended for diagnostics.
    /// </summary>
    private static string BuildPullErrorMessage(string pullError)
    {
        string raw = (pullError ?? string.Empty).Trim();
        string lower = raw.ToLowerInvariant();

        if (lower.Contains("toomanyrequests") || lower.Contains("pull rate limit") || lower.Contains("429"))
        {
            return "Docker registry pull rate limit reached. If a registry is configured, its "
                + "credentials may be invalid or expired (the pull then falls back to anonymous) — "
                + "re-authenticate in Settings → Registry Management. Otherwise wait for the limit to "
                + $"reset or add credentials to raise it. Details: {raw}";
        }

        if (lower.Contains("unauthorized") || lower.Contains("authentication required")
            || lower.Contains("access denied") || lower.Contains("denied: requested access"))
        {
            return "Registry authentication failed — the stored credentials are missing, invalid, or "
                + $"lack access to this image. Re-authenticate in Settings → Registry Management. Details: {raw}";
        }

        return $"Failed to pull images: {raw}";
    }

}
