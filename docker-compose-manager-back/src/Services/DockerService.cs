using Docker.DotNet;
using Docker.DotNet.Models;
using docker_compose_manager_back.DTOs;

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
            var containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = showAll });

            return containers.Select(c => new ContainerDto(
                c.ID,
                c.Names.FirstOrDefault() ?? "unknown",
                c.Image,
                c.Status,
                c.State,
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
            var container = await _dockerClient.Containers.InspectContainerAsync(containerId);

            var mounts = container.Mounts?.Select(m => new MountDto(
                m.Type,
                m.Source,
                m.Destination,
                !m.RW
            )).ToList();

            var networks = container.NetworkSettings?.Networks?.Keys.ToList();

            var ports = container.NetworkSettings?.Ports?
                .Where(p => p.Value != null)
                .ToDictionary(
                    p => p.Key,
                    p => string.Join(", ", p.Value.Select(b => $"{b.HostIP}:{b.HostPort}"))
                );

            var envDict = container.Config?.Env?
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
}
