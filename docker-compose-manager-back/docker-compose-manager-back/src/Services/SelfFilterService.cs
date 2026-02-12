namespace docker_compose_manager_back.Services;

/// <summary>
/// Service for filtering the application's own containers and compose project
/// from all lists and preventing operations on them.
/// </summary>
public interface ISelfFilterService
{
    /// <summary>
    /// Checks if the given container ID belongs to this application.
    /// Handles both 12-char and 64-char container IDs via StartsWith comparison.
    /// </summary>
    Task<bool> IsSelfContainerAsync(string containerId);

    /// <summary>
    /// Checks if the given project name is the compose project running this application.
    /// </summary>
    Task<bool> IsSelfProjectAsync(string projectName);

    /// <summary>
    /// Gets the compose project name of this application, or null if not running via compose.
    /// </summary>
    Task<string?> GetSelfProjectNameAsync();

    /// <summary>
    /// Gets the container ID of this application, or null if not running in Docker.
    /// </summary>
    Task<string?> GetSelfContainerIdAsync();
}

public class SelfFilterService : ISelfFilterService
{
    private readonly IComposeFileDetectorService _detectorService;
    private readonly ILogger<SelfFilterService> _logger;

    public SelfFilterService(
        IComposeFileDetectorService detectorService,
        ILogger<SelfFilterService> logger)
    {
        _detectorService = detectorService;
        _logger = logger;
    }

    public async Task<bool> IsSelfContainerAsync(string containerId)
    {
        if (string.IsNullOrEmpty(containerId))
            return false;

        ComposeDetectionResult detection = await _detectorService.GetComposeDetectionResultAsync();

        if (!detection.IsRunningInDocker || string.IsNullOrEmpty(detection.ContainerId))
            return false;

        // Handle 12-char vs 64-char IDs with bidirectional StartsWith
        return containerId.StartsWith(detection.ContainerId, StringComparison.OrdinalIgnoreCase)
            || detection.ContainerId.StartsWith(containerId, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsSelfProjectAsync(string projectName)
    {
        if (string.IsNullOrEmpty(projectName))
            return false;

        ComposeDetectionResult detection = await _detectorService.GetComposeDetectionResultAsync();

        if (!detection.IsRunningViaCompose || string.IsNullOrEmpty(detection.ProjectName))
            return false;

        return detection.ProjectName.Equals(projectName, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetSelfProjectNameAsync()
    {
        ComposeDetectionResult detection = await _detectorService.GetComposeDetectionResultAsync();
        return detection.IsRunningViaCompose ? detection.ProjectName : null;
    }

    public async Task<string?> GetSelfContainerIdAsync()
    {
        ComposeDetectionResult detection = await _detectorService.GetComposeDetectionResultAsync();
        return detection.IsRunningInDocker ? detection.ContainerId : null;
    }
}
