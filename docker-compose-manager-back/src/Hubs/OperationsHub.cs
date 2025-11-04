using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace docker_compose_manager_back.Hubs;

[Authorize]
public class OperationsHub : Hub
{
    private readonly ILogger<OperationsHub> _logger;

    public OperationsHub(ILogger<OperationsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to OperationsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from OperationsHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to operation updates
    /// </summary>
    public async Task SubscribeToOperation(string operationId)
    {
        string groupName = $"operation-{operationId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} subscribed to operation {OperationId}", Context.ConnectionId, operationId);
    }

    /// <summary>
    /// Unsubscribe from operation updates
    /// </summary>
    public async Task UnsubscribeFromOperation(string operationId)
    {
        string groupName = $"operation-{operationId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from operation {OperationId}", Context.ConnectionId, operationId);
    }
}
