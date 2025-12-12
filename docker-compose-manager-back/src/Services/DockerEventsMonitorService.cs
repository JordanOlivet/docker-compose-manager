using Docker.DotNet;
using Docker.DotNet.Models;
using docker_compose_manager_back.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Background service that monitors Docker events and broadcasts container state changes via SignalR
/// </summary>
public class DockerEventsMonitorService : BackgroundService
{
    private readonly DockerClient _dockerClient;
    private readonly IHubContext<OperationsHub> _hubContext;
    private readonly ILogger<DockerEventsMonitorService> _logger;

    public DockerEventsMonitorService(
        IConfiguration configuration,
        IHubContext<OperationsHub> hubContext,
        ILogger<DockerEventsMonitorService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        string? dockerHost = configuration["Docker:Host"];
        if (string.IsNullOrEmpty(dockerHost))
        {
            throw new ArgumentException("Docker host is not configured. Set 'Docker__Host' environment variable.");
        }

        try
        {
            _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
            _logger.LogInformation("DockerEventsMonitorService initialized with host: {DockerHost}", dockerHost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Docker client in DockerEventsMonitorService");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DockerEventsMonitorService is starting");

        // Wait a bit before starting to ensure all services are initialized
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorDockerEventsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("DockerEventsMonitorService is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Docker events. Restarting in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("DockerEventsMonitorService has stopped");
    }

    private async Task MonitorDockerEventsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting to monitor Docker events...");

        var parameters = new ContainerEventsParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["type"] = new Dictionary<string, bool> { ["container"] = true }
            }
        };

        Progress<Message> progress = new Progress<Message>(async (message) =>
        {
            try
            {
                await HandleDockerEventAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Docker event");
            }
        });

        await _dockerClient.System.MonitorEventsAsync(parameters, progress, stoppingToken);
    }

    private async Task HandleDockerEventAsync(Message message)
    {
        // Only handle container events
        if (message.Type != "container")
        {
            return;
        }

        // Container events we care about
        string[] relevantActions = new[]
        {
            "start", "stop", "die", "kill", "pause", "unpause", "restart",
            "create", "destroy", "remove", "rename"
        };

        if (!relevantActions.Contains(message.Action))
        {
            return;
        }

        string containerId = message.Actor?.ID ?? "unknown";
        string containerName = message.Actor?.Attributes != null && message.Actor.Attributes.TryGetValue("name", out string? name)
            ? name
            : "unknown";

        _logger.LogInformation(
            "Container event detected - Action: {Action}, Container: {ContainerName} ({ContainerId})",
            message.Action,
            containerName,
            containerId.Substring(0, Math.Min(12, containerId.Length))
        );

        // Debug: Log all container labels to help diagnose compose project detection
        if (message.Actor?.Attributes != null)
        {
            string labels = string.Join(", ", message.Actor.Attributes.Keys);
            _logger.LogDebug("Container {ContainerName} labels: {Labels}", containerName, labels);
        }

        // Broadcast container state change to all connected SignalR clients
        await _hubContext.Clients.All.SendAsync("ContainerStateChanged", new
        {
            action = message.Action,
            containerId = containerId,
            containerName = containerName,
            timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Time).DateTime
        });

        // Check if this container belongs to a Docker Compose project
        if (message.Actor?.Attributes != null &&
            message.Actor.Attributes.TryGetValue("com.docker.compose.project", out string? projectName))
        {
            _logger.LogInformation(
                "Compose project event detected - Project: {ProjectName}, Action: {Action}, Service: {ServiceName}",
                projectName,
                message.Action,
                message.Actor.Attributes.TryGetValue("com.docker.compose.service", out string? serviceName) ? serviceName : "unknown"
            );

            // Broadcast compose project state change to all connected SignalR clients
            await _hubContext.Clients.All.SendAsync("ComposeProjectStateChanged", new
            {
                projectName = projectName,
                action = message.Action,
                serviceName = message.Actor.Attributes.TryGetValue("com.docker.compose.service", out string? svc) ? svc : null,
                containerId = containerId,
                containerName = containerName,
                timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Time).DateTime
            });

            _logger.LogDebug("Broadcasted compose project state change to all SignalR clients");
        }
    }

    public override void Dispose()
    {
        _dockerClient?.Dispose();
        base.Dispose();
    }
}
