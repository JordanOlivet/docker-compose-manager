using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using docker_compose_manager_back.Services;

namespace docker_compose_manager_back.Hubs;

[Authorize]
public class LogsHub : Hub
{
    private readonly ComposeService _composeService;
    private readonly DockerService _dockerService;
    private readonly ILogger<LogsHub> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _activeStreams = new();

    public LogsHub(
        ComposeService composeService,
        DockerService dockerService,
        ILogger<LogsHub> logger)
    {
        _composeService = composeService;
        _dockerService = dockerService;
        _logger = logger;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;

        // Cancel any active streams for this connection
        if (_activeStreams.TryGetValue(connectionId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            _activeStreams.Remove(connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Streams logs from a compose project
    /// </summary>
    public async Task StreamComposeLogs(string projectPath, string? serviceName = null/*, int tail = 100*/)
    {
        string connectionId = Context.ConnectionId;

        try
        {
            CancellationTokenSource cts = new();
            _activeStreams[connectionId] = cts;

            _logger.LogInformation("Starting compose log stream for {ProjectPath}", projectPath);

            // Get initial logs
            var (success, output, error) = await _composeService.GetLogsAsync(
                projectPath,
                serviceName,
                null,
                follow: false,
                cts.Token
            );

            if (success)
            {
                await Clients.Caller.SendAsync("ReceiveLogs", output, cts.Token);
            }
            else
            {
                await Clients.Caller.SendAsync("LogError", error ?? "Failed to get logs", cts.Token);
            }

            // In a production system, you would implement proper log streaming
            // For now, we just send the initial logs
            await Clients.Caller.SendAsync("StreamComplete", cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Log stream cancelled for {ProjectPath}", projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming logs for {ProjectPath}", projectPath);
            await Clients.Caller.SendAsync("LogError", "Error streaming logs");
        }
        finally
        {
            _activeStreams.Remove(connectionId);
        }
    }

    /// <summary>
    /// Streams logs from a container
    /// </summary>
    public async Task StreamContainerLogs(string containerId, int tail = 100)
    {
        string connectionId = Context.ConnectionId;

        try
        {
            CancellationTokenSource cts = new();
            _activeStreams[connectionId] = cts;

            _logger.LogInformation("Starting container log stream for {ContainerId}", containerId);

            // Get container logs
            var logs = await _dockerService.GetContainerLogsAsync(containerId, tail, timestamps: true);

            if (logs != null && logs.Any())
            {
                // Send logs line by line to simulate streaming
                foreach (var logLine in logs)
                {
                    if (cts.Token.IsCancellationRequested)
                        break;

                    await Clients.Caller.SendAsync("ReceiveLogs", logLine, cts.Token);
                }
            }

            await Clients.Caller.SendAsync("StreamComplete", cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Log stream cancelled for container {ContainerId}", containerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming container logs for {ContainerId}", containerId);
            await Clients.Caller.SendAsync("LogError", "Error streaming logs");
        }
        finally
        {
            _activeStreams.Remove(connectionId);
        }
    }

    /// <summary>
    /// Stops streaming logs
    /// </summary>
    public Task StopStream()
    {
        string connectionId = Context.ConnectionId;

        if (_activeStreams.TryGetValue(connectionId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            _activeStreams.Remove(connectionId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends operation progress updates
    /// </summary>
    public async Task SubscribeToOperation(string operationId)
    {
        string groupName = $"operation-{operationId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to operation {OperationId}", Context.ConnectionId, operationId);
    }

    /// <summary>
    /// Unsubscribes from operation updates
    /// </summary>
    public async Task UnsubscribeFromOperation(string operationId)
    {
        string groupName = $"operation-{operationId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from operation {OperationId}", Context.ConnectionId, operationId);
    }
}
