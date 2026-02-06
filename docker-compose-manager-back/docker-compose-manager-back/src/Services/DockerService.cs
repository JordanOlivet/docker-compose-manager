using Docker.DotNet;
using Docker.DotNet.Models;
using docker_compose_manager_back.DTOs;
using docker_compose_manager_back.src.Utils;

namespace docker_compose_manager_back.Services;

public class DockerService
{
    private readonly DockerClient _dockerClient;
    private readonly ILogger<DockerService> _logger;

    public DockerService(IConfiguration configuration, ILogger<DockerService> logger)
    {
        _logger = logger;

        string? dockerHost = configuration["Docker:Host"];

        if (string.IsNullOrEmpty(dockerHost))
        {
            throw new ArgumentException("Unable to initialize Docker client with an empty docker host. You have to set it with the env var 'Docker__Host' in the environment section of the compose file.");
        }

        try
        {
            _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
            _logger.LogDebug("Docker client initialized with host: {DockerHost}", dockerHost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Docker client with host: {DockerHost}", dockerHost);
            throw;
        }
    }

    public async Task<List<ContainerDto>> ListContainersAsync(bool showAll = true)
    {
        try
        {
            IList<ContainerListResponse> containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = showAll });

            return containers.Select(c => new ContainerDto(
                c.ID,
                NormalizeName(c.Names.FirstOrDefault()),
                c.Image,
                c.Status,
                c.State.ToEntityState().ToStateString(),
                c.Created,
                c.Labels != null ? new Dictionary<string, string>(c.Labels) : null
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing containers");
            throw;
        }
    }

    public async Task<ContainerDetailsDto?> GetContainerDetailsAsync(string containerId)
    {
        try
        {
            ContainerInspectResponse container = await _dockerClient.Containers.InspectContainerAsync(containerId);

            List<MountDto>? mounts = container.Mounts?.Select(m => new MountDto(
                m.Type,
                m.Source,
                m.Destination,
                !m.RW
            )).ToList();

            List<string>? networks = container.NetworkSettings?.Networks?.Keys.ToList();

            Dictionary<string, string>? ports = container.NetworkSettings?.Ports?
                .Where(p => p.Value != null)
                .ToDictionary(
                    p => p.Key,
                    p => string.Join(", ", p.Value.Select(b => $"{b.HostIP}:{b.HostPort}"))
                );

            Dictionary<string, string>? envDict = container.Config?.Env?
                .Select(e => e.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1]);

            return new ContainerDetailsDto(
                container.ID,
                NormalizeName(container.Name),
                container.Config?.Image ?? "unknown",
                container.State?.Status ?? "unknown",
                container.State?.Status?.ToEntityState().ToStateString() ?? "Unknown",
                container.Created,
                container.Config?.Labels != null ? new Dictionary<string, string>(container.Config.Labels) : null,
                envDict,
                mounts,
                networks,
                ports
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container details for {ContainerId}", containerId);
            return null;
        }
    }

    public async Task<bool> StartContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
            _logger.LogDebug("Container {ContainerId} started", containerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting container {ContainerId}", containerId);
            return false;
        }
    }

    public async Task<bool> StopContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
            _logger.LogDebug("Container {ContainerId} stopped", containerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping container {ContainerId}", containerId);
            return false;
        }
    }

    public async Task<bool> RestartContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.RestartContainerAsync(containerId, new ContainerRestartParameters());
            _logger.LogDebug("Container {ContainerId} restarted", containerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting container {ContainerId}", containerId);
            return false;
        }
    }

    public async Task<bool> RemoveContainerAsync(string containerId, bool force = false)
    {
        try
        {
            await _dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = force });
            _logger.LogDebug("Container {ContainerId} removed", containerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing container {ContainerId}", containerId);
            return false;
        }
    }

    public async Task<List<string>> GetContainerLogsAsync(string containerId, int tail = 100, bool timestamps = false)
    {
        try
        {
            ContainerLogsParameters parameters = new()
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = tail.ToString(),
                Timestamps = timestamps
            };

            MultiplexedStream logs = await _dockerClient.Containers.GetContainerLogsAsync(
                containerId,
                true,
                parameters,
                CancellationToken.None
            );

            List<string> logLines = new();
            (string stdout, string stderr) = await logs.ReadOutputToEndAsync(CancellationToken.None);

            // Combine stdout and stderr
            if (!string.IsNullOrEmpty(stdout))
            {
                logLines.AddRange(stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            if (!string.IsNullOrEmpty(stderr))
            {
                logLines.AddRange(stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }

            _logger.LogDebug("Retrieved {LineCount} log lines from container {ContainerId}", logLines.Count, containerId);
            return logLines;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting logs for container {ContainerId}", containerId);
            throw;
        }
    }

    public async Task<ContainerStatsDto?> GetContainerStatsAsync(string containerId)
    {
        try
        {
            // First, check if container exists and is running
            ContainerInspectResponse? container = null;
            try
            {
                container = await _dockerClient.Containers.InspectContainerAsync(containerId);
            }
            catch (DockerContainerNotFoundException)
            {
                // Container doesn't exist - silently return null
                return null;
            }

            // If container is not running, don't try to get stats
            if (container?.State?.Running != true)
            {
                return null;
            }

            ContainerStatsParameters statsParameters = new()
            {
                Stream = false // Get one-time stats, not streaming
            };

            Progress<ContainerStatsResponse> statsProgress = new();
            ContainerStatsResponse? lastStats = null;

            statsProgress.ProgressChanged += (sender, stats) =>
            {
                lastStats = stats;
            };

            await _dockerClient.Containers.GetContainerStatsAsync(
                containerId,
                statsParameters,
                statsProgress,
                CancellationToken.None
            );

            if (lastStats == null)
            {
                // No stats received, but container exists - just return null silently
                return null;
            }

            // Calculate CPU percentage
            ulong cpuDelta = lastStats.CPUStats.CPUUsage.TotalUsage - lastStats.PreCPUStats.CPUUsage.TotalUsage;
            ulong systemDelta = lastStats.CPUStats.SystemUsage - lastStats.PreCPUStats.SystemUsage;
            double cpuPercent = 0.0;
            if (systemDelta > 0 && cpuDelta > 0)
            {
                int cpuCount = lastStats.CPUStats.CPUUsage.PercpuUsage?.Count() ?? 1;
                cpuPercent = (cpuDelta / (double)systemDelta) * cpuCount * 100.0;
            }

            // Calculate memory usage
            ulong memoryUsage = lastStats.MemoryStats.Usage;
            ulong memoryLimit = lastStats.MemoryStats.Limit;
            double memoryPercent = memoryLimit > 0 ? (memoryUsage / (double)memoryLimit) * 100.0 : 0;

            // Calculate network I/O
            long networkRx = lastStats.Networks?.Values.Sum(n => (long)n.RxBytes) ?? 0;
            long networkTx = lastStats.Networks?.Values.Sum(n => (long)n.TxBytes) ?? 0;

            // Calculate disk I/O - simplified (DiskIO property might vary by Docker.DotNet version)
            long diskRead = 0L;
            long diskWrite = 0L;

            return new ContainerStatsDto(
                cpuPercent,
                memoryUsage,
                memoryLimit,
                memoryPercent,
                networkRx,
                networkTx,
                diskRead,
                diskWrite
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats for container {ContainerId}", containerId);
            return null;
        }
    }
    /// <summary>
    /// Lists containers belonging to a specific Docker Compose project.
    /// Uses the com.docker.compose.project label to filter containers.
    /// </summary>
    /// <param name="projectName">The compose project name</param>
    /// <param name="showAll">Include stopped containers</param>
    /// <returns>List of containers with compose-specific metadata</returns>
    public async Task<List<ComposeServiceDto>> ListContainersByComposeProjectAsync(string projectName, bool showAll = true)
    {
        try
        {
            // Filter containers by compose project label
            var filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool>
                {
                    [$"com.docker.compose.project={projectName}"] = true
                }
            };

            IList<ContainerListResponse> containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = showAll,
                    Filters = filters
                });

            return containers.Select(c => {
                // Use the actual container name (consistent with containers page)
                string containerName = NormalizeName(c.Names.FirstOrDefault());

                // Parse ports
                var ports = c.Ports?
                    .Where(p => p.PublicPort > 0)
                    .Select(p => $"{p.PublicPort}:{p.PrivatePort}")
                    .ToList() ?? new List<string>();

                // Get health status from labels or status string
                string? health = null;
                if (c.Status?.Contains("(healthy)") == true) health = "healthy";
                else if (c.Status?.Contains("(unhealthy)") == true) health = "unhealthy";
                else if (c.Status?.Contains("(health:") == true) health = "starting";

                return new ComposeServiceDto(
                    Id: c.ID,
                    Name: containerName,
                    Image: c.Image,
                    State: c.State.ToEntityState().ToStateString(),
                    Status: c.Status ?? "",
                    Ports: ports,
                    Health: health
                );
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing containers for compose project {ProjectName}", projectName);
            return new List<ComposeServiceDto>();
        }
    }

    /// <summary>
    /// Gets Docker daemon version information
    /// </summary>
    public async Task<(string? version, string? apiVersion)> GetVersionAsync()
    {
        try
        {
            var versionResponse = await _dockerClient.System.GetVersionAsync();
            return (versionResponse.Version, versionResponse.APIVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Docker version");
            throw;
        }
    }

    /// <summary>
    /// Normalizes a docker container name by removing the leading '/' that the Docker API returns.
    /// Returns "unknown" if the provided name is null or whitespace.
    /// </summary>
    private static string NormalizeName(string? rawName)
        => string.IsNullOrWhiteSpace(rawName) ? "unknown" : rawName.TrimStart('/');
}
