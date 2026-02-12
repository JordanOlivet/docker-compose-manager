using Docker.DotNet.Models;

namespace docker_compose_manager_back.Services;

/// <summary>
/// Handles Docker events by filtering relevant container actions and broadcasting state changes via SSE.
/// Extracted from DockerEventsMonitorService to enable unit testing.
/// </summary>
public class DockerEventHandlerService
{
    private readonly SseConnectionManagerService _sseManager;
    private readonly ILogger<DockerEventHandlerService> _logger;

    private static readonly string[] RelevantActions =
    [
        "start", "stop", "die", "kill", "pause", "unpause", "restart",
        "create", "destroy", "remove", "rename"
    ];

    public DockerEventHandlerService(
        SseConnectionManagerService sseManager,
        ILogger<DockerEventHandlerService> logger)
    {
        _sseManager = sseManager;
        _logger = logger;
    }

    public async Task HandleAsync(Message message)
    {
        // Only handle container events
        if (message.Type != "container")
        {
            return;
        }

        if (!RelevantActions.Contains(message.Action))
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

        // Broadcast container state change to all connected SSE clients
        await _sseManager.BroadcastAsync("ContainerStateChanged", new
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

            // Broadcast compose project state change to all connected SSE clients
            await _sseManager.BroadcastAsync("ComposeProjectStateChanged", new
            {
                projectName = projectName,
                action = message.Action,
                serviceName = message.Actor.Attributes.TryGetValue("com.docker.compose.service", out string? svc) ? svc : null,
                containerId = containerId,
                containerName = containerName,
                timestamp = DateTimeOffset.FromUnixTimeSeconds(message.Time).DateTime
            });

            _logger.LogDebug("Broadcasted compose project state change to all SSE clients");
        }
    }
}
