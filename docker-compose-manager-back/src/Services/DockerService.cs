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

        // Auto-detect Docker host based on platform if not configured
        string? dockerHost = configuration["Docker:Host"];

        if (string.IsNullOrEmpty(dockerHost))
        {
            // Auto-detect based on platform
            dockerHost = OperatingSystem.IsWindows()
                ? "npipe://./pipe/docker_engine"
                : "unix:///var/run/docker.sock";
            _logger.LogInformation("Auto-detected Docker host for platform: {DockerHost}", dockerHost);
        }

        try
        {
            _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
            _logger.LogInformation("Docker client initialized with host: {DockerHost}", dockerHost);
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
                c.Names.FirstOrDefault() ?? "unknown",
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
                container.Name,
                container.Config?.Image ?? "unknown",
                container.State?.Status ?? "unknown",
                container.State?.Status ?? "unknown",
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
            _logger.LogInformation("Container {ContainerId} started", containerId);
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
            _logger.LogInformation("Container {ContainerId} stopped", containerId);
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
            _logger.LogInformation("Container {ContainerId} restarted", containerId);
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
            _logger.LogInformation("Container {ContainerId} removed", containerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing container {ContainerId}", containerId);
            return false;
        }
    }

    [Obsolete]
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

            Stream logs = await _dockerClient.Containers.GetContainerLogsAsync(
                containerId,
                parameters,
                CancellationToken.None
            );

            List<string> logLines = new();
            using (StreamReader reader = new(logs))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    // Docker API prepends 8 bytes header, skip it if present
                    if (line.Length > 8)
                    {
                        logLines.Add(line.Substring(8));
                    }
                    else
                    {
                        logLines.Add(line);
                    }
                }
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
                _logger.LogWarning("No stats received for container {ContainerId}", containerId);
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

            // Calculate block I/O - simplified (BlockIO property might vary by Docker.DotNet version)
            long blockRead = 0L;
            long blockWrite = 0L;

            return new ContainerStatsDto(
                cpuPercent,
                memoryUsage,
                memoryLimit,
                memoryPercent,
                networkRx,
                networkTx,
                blockRead,
                blockWrite
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats for container {ContainerId}", containerId);
            return null;
        }
    }
}
