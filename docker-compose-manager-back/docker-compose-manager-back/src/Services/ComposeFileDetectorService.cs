using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;

namespace docker_compose_manager_back.Services;

public interface IComposeFileDetectorService
{
    /// <summary>
    /// Attempts to detect the compose file path for the current container.
    /// </summary>
    /// <returns>The compose file path if detected, null otherwise.</returns>
    Task<string?> DetectComposeFilePathAsync();

    /// <summary>
    /// Gets information about the current container's compose configuration.
    /// </summary>
    Task<ComposeDetectionResult> GetComposeDetectionResultAsync();
}

public record ComposeDetectionResult(
    bool IsRunningInDocker,
    bool IsRunningViaCompose,
    string? ComposeFilePath,
    string? WorkingDirectory,
    string? ProjectName,
    string? ContainerId,
    string? DetectionError
);

public class ComposeFileDetectorService : IComposeFileDetectorService
{
    private readonly DockerClient _dockerClient;
    private readonly ILogger<ComposeFileDetectorService> _logger;
    private ComposeDetectionResult? _cachedResult;

    public ComposeFileDetectorService(
        IConfiguration configuration,
        ILogger<ComposeFileDetectorService> logger)
    {
        _logger = logger;

        string? dockerHost = configuration["Docker:Host"];
        if (string.IsNullOrEmpty(dockerHost))
        {
            _logger.LogWarning("Docker host not configured. Compose file auto-detection will be unavailable.");
            _dockerClient = null!;
        }
        else
        {
            try
            {
                _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
                _logger.LogDebug("ComposeFileDetectorService initialized with Docker host: {DockerHost}", dockerHost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Docker client for compose file detection");
                _dockerClient = null!;
            }
        }
    }

    public async Task<string?> DetectComposeFilePathAsync()
    {
        var result = await GetComposeDetectionResultAsync();
        return result.ComposeFilePath;
    }

    public async Task<ComposeDetectionResult> GetComposeDetectionResultAsync()
    {
        // Return cached result if available
        if (_cachedResult != null)
        {
            return _cachedResult;
        }

        // Check if Docker client is available
        if (_dockerClient == null)
        {
            _cachedResult = new ComposeDetectionResult(
                IsRunningInDocker: false,
                IsRunningViaCompose: false,
                ComposeFilePath: null,
                WorkingDirectory: null,
                ProjectName: null,
                ContainerId: null,
                DetectionError: "Docker client not available. Check Docker:Host configuration."
            );
            return _cachedResult;
        }

        try
        {
            // Check if running in Docker
            string? containerId = GetCurrentContainerId();
            if (string.IsNullOrEmpty(containerId))
            {
                _cachedResult = new ComposeDetectionResult(
                    IsRunningInDocker: false,
                    IsRunningViaCompose: false,
                    ComposeFilePath: null,
                    WorkingDirectory: null,
                    ProjectName: null,
                    ContainerId: null,
                    DetectionError: "Not running in a Docker container"
                );
                return _cachedResult;
            }

            _logger.LogDebug("Detected container ID: {ContainerId}", containerId);

            // Inspect the container to get labels
            ContainerInspectResponse inspection = await _dockerClient.Containers.InspectContainerAsync(containerId);

            if (inspection.Config?.Labels == null)
            {
                _cachedResult = new ComposeDetectionResult(
                    IsRunningInDocker: true,
                    IsRunningViaCompose: false,
                    ComposeFilePath: null,
                    WorkingDirectory: null,
                    ProjectName: null,
                    ContainerId: containerId,
                    DetectionError: "Container has no labels"
                );
                return _cachedResult;
            }

            IDictionary<string, string> labels = inspection.Config.Labels;

            // Check for docker-compose labels
            string? configFiles = labels.TryGetValue("com.docker.compose.project.config_files", out var cf) ? cf : null;
            string? workingDir = labels.TryGetValue("com.docker.compose.project.working_dir", out var wd) ? wd : null;
            string? projectName = labels.TryGetValue("com.docker.compose.project", out var pn) ? pn : null;

            if (string.IsNullOrEmpty(configFiles))
            {
                _cachedResult = new ComposeDetectionResult(
                    IsRunningInDocker: true,
                    IsRunningViaCompose: false,
                    ComposeFilePath: null,
                    WorkingDirectory: workingDir,
                    ProjectName: projectName,
                    ContainerId: containerId,
                    DetectionError: "Container was not started via docker-compose (no config_files label)"
                );
                return _cachedResult;
            }

            // config_files can contain multiple files separated by comma, take the first one
            string composeFilePath = configFiles.Split(',')[0].Trim();

            _logger.LogDebug(
                "Detected compose file: {ComposeFile}, Working dir: {WorkingDir}, Project: {Project}",
                composeFilePath, workingDir, projectName);

            _cachedResult = new ComposeDetectionResult(
                IsRunningInDocker: true,
                IsRunningViaCompose: true,
                ComposeFilePath: composeFilePath,
                WorkingDirectory: workingDir,
                ProjectName: projectName,
                ContainerId: containerId,
                DetectionError: null
            );
            return _cachedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting compose file path");
            _cachedResult = new ComposeDetectionResult(
                IsRunningInDocker: false,
                IsRunningViaCompose: false,
                ComposeFilePath: null,
                WorkingDirectory: null,
                ProjectName: null,
                ContainerId: null,
                DetectionError: ex.Message
            );
            return _cachedResult;
        }
    }

    /// <summary>
    /// Gets the current container ID by reading from cgroup or hostname.
    /// </summary>
    private string? GetCurrentContainerId()
    {
        // Method 1: Try reading from /proc/self/cgroup (Linux)
        string? containerId = TryGetContainerIdFromCgroup();
        if (!string.IsNullOrEmpty(containerId))
        {
            return containerId;
        }

        // Method 2: Try reading from /proc/1/cpuset (Linux)
        containerId = TryGetContainerIdFromCpuset();
        if (!string.IsNullOrEmpty(containerId))
        {
            return containerId;
        }

        // Method 3: Try using HOSTNAME environment variable (often set to container ID)
        containerId = TryGetContainerIdFromHostname();
        if (!string.IsNullOrEmpty(containerId))
        {
            return containerId;
        }

        // Method 4: Check for /.dockerenv file (indicates we're in Docker, but need to find ID)
        if (File.Exists("/.dockerenv"))
        {
            // We're in Docker but couldn't determine ID, try hostname
            string hostname = Environment.MachineName;
            if (!string.IsNullOrEmpty(hostname) && hostname.Length >= 12)
            {
                return hostname;
            }
        }

        return null;
    }

    private string? TryGetContainerIdFromCgroup()
    {
        try
        {
            if (!File.Exists("/proc/self/cgroup"))
            {
                return null;
            }

            string[] lines = File.ReadAllLines("/proc/self/cgroup");
            foreach (string line in lines)
            {
                // Format: hierarchy-ID:controller-list:cgroup-path
                // Docker container IDs appear in the cgroup path
                // Example: 12:memory:/docker/abc123def456...
                string[] parts = line.Split(':');
                if (parts.Length >= 3)
                {
                    string cgroupPath = parts[2];

                    // Look for docker container ID pattern (64 hex characters)
                    if (cgroupPath.Contains("/docker/"))
                    {
                        string[] pathParts = cgroupPath.Split('/');
                        foreach (string part in pathParts)
                        {
                            if (part.Length == 64 && IsHexString(part))
                            {
                                return part;
                            }
                        }
                    }

                    // Also check for containerd pattern
                    if (cgroupPath.Contains("/containerd/"))
                    {
                        string[] pathParts = cgroupPath.Split('/');
                        foreach (string part in pathParts)
                        {
                            if (part.Length == 64 && IsHexString(part))
                            {
                                return part;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read container ID from /proc/self/cgroup");
        }

        return null;
    }

    private string? TryGetContainerIdFromCpuset()
    {
        try
        {
            if (!File.Exists("/proc/1/cpuset"))
            {
                return null;
            }

            string cpuset = File.ReadAllText("/proc/1/cpuset").Trim();
            // Format: /docker/abc123def456... or /kubepods/...
            if (cpuset.Contains("/docker/"))
            {
                string[] parts = cpuset.Split('/');
                string lastPart = parts[^1];
                if (lastPart.Length == 64 && IsHexString(lastPart))
                {
                    return lastPart;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read container ID from /proc/1/cpuset");
        }

        return null;
    }

    private string? TryGetContainerIdFromHostname()
    {
        try
        {
            string? hostname = Environment.GetEnvironmentVariable("HOSTNAME");
            if (!string.IsNullOrEmpty(hostname))
            {
                // Docker typically sets HOSTNAME to the first 12 characters of the container ID
                // or the full 64-character ID
                if ((hostname.Length == 12 || hostname.Length == 64) && IsHexString(hostname))
                {
                    return hostname;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read container ID from HOSTNAME");
        }

        return null;
    }

    private static bool IsHexString(string str)
    {
        foreach (char c in str)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
            {
                return false;
            }
        }
        return true;
    }
}
