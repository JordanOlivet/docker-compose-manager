using System.Collections.Concurrent;
using System.Text.Json;

namespace docker_compose_manager_back.Services;

public class SseClient
{
    public required HttpResponse Response { get; init; }
    public required string ConnectionId { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Thread-safe singleton that manages active SSE connections and broadcasts events to all clients.
/// Replaces SignalR's IHubContext for server-to-client communication.
/// </summary>
public class SseConnectionManagerService
{
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();
    private readonly SemaphoreSlim _broadcastLock = new(1, 1);
    private readonly ILogger<SseConnectionManagerService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SseConnectionManagerService(ILogger<SseConnectionManagerService> logger)
    {
        _logger = logger;
    }

    public string AddClient(SseClient client)
    {
        _clients[client.ConnectionId] = client;
        _logger.LogDebug("SSE client connected: {ConnectionId}. Total clients: {Count}",
            client.ConnectionId, _clients.Count);
        return client.ConnectionId;
    }

    public void RemoveClient(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out _))
        {
            _logger.LogDebug("SSE client disconnected: {ConnectionId}. Total clients: {Count}",
                connectionId, _clients.Count);
        }
    }

    /// <summary>
    /// Broadcasts an SSE event to all connected clients.
    /// Disconnected clients are automatically removed.
    /// </summary>
    public async Task BroadcastAsync(string eventType, object data)
    {
        if (_clients.IsEmpty)
            return;

        string json = JsonSerializer.Serialize(data, JsonOptions);
        string sseMessage = $"event: {eventType}\ndata: {json}\n\n";

        // Serialize broadcasts to prevent concurrent writes to the same HttpResponse streams.
        // Fire-and-forget callers (e.g. pull progress) can trigger overlapping broadcasts,
        // and HttpResponse.WriteAsync is not thread-safe.
        await _broadcastLock.WaitAsync();
        try
        {
            List<string> disconnected = [];

            foreach (var (connectionId, client) in _clients)
            {
                try
                {
                    if (client.CancellationToken.IsCancellationRequested)
                    {
                        disconnected.Add(connectionId);
                        continue;
                    }

                    await client.Response.WriteAsync(sseMessage, client.CancellationToken);
                    await client.Response.Body.FlushAsync(client.CancellationToken);
                }
                catch (Exception)
                {
                    disconnected.Add(connectionId);
                }
            }

            foreach (string id in disconnected)
            {
                RemoveClient(id);
            }
        }
        finally
        {
            _broadcastLock.Release();
        }
    }

    /// <summary>
    /// Writes a raw SSE message to a specific client under the broadcast lock,
    /// preventing concurrent writes with BroadcastAsync from corrupting the stream.
    /// Used by the SSE controller for the initial connected event and heartbeats.
    /// </summary>
    public async Task WriteToClientAsync(string connectionId, string message)
    {
        await _broadcastLock.WaitAsync();
        try
        {
            if (!_clients.TryGetValue(connectionId, out var client))
                return;

            if (client.CancellationToken.IsCancellationRequested)
            {
                RemoveClient(connectionId);
                return;
            }

            await client.Response.WriteAsync(message, client.CancellationToken);
            await client.Response.Body.FlushAsync(client.CancellationToken);
        }
        catch (Exception)
        {
            RemoveClient(connectionId);
        }
        finally
        {
            _broadcastLock.Release();
        }
    }

    public int ClientCount => _clients.Count;
}
