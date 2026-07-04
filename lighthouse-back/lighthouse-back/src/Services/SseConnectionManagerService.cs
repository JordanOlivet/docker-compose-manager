using System.Collections.Concurrent;
using System.Text.Json;

namespace Lighthouse.Services;

public class SseClient
{
    public required HttpResponse Response { get; init; }
    public required string ConnectionId { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Serializes writes to this client's response stream. Each client has its own lock so
    /// a slow client only blocks its own writes, not broadcasts to everyone else.
    /// </summary>
    public SemaphoreSlim WriteLock { get; } = new(1, 1);
}

/// <summary>
/// Thread-safe singleton that manages active SSE connections and broadcasts events to all clients.
/// Replaces SignalR's IHubContext for server-to-client communication.
/// </summary>
public class SseConnectionManagerService
{
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();
    private readonly ILogger<SseConnectionManagerService> _logger;

    // A single write to a client may not hang forever: a stalled connection is dropped
    // instead of holding up its own queue (broadcasts to other clients are unaffected).
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);

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
    /// Broadcasts an SSE event to all connected clients concurrently.
    /// Each client's write is serialized by its own lock and bounded by a timeout, so a
    /// slow or stalled client is dropped without holding up delivery to the others.
    /// </summary>
    public async Task BroadcastAsync(string eventType, object data)
    {
        if (_clients.IsEmpty)
            return;

        string json = JsonSerializer.Serialize(data, JsonOptions);
        string sseMessage = $"event: {eventType}\ndata: {json}\n\n";

        // _clients.Values is a snapshot, so removals during writes don't disturb iteration.
        await Task.WhenAll(_clients.Values.Select(client => WriteToClientInternalAsync(client, sseMessage)));
    }

    /// <summary>
    /// Writes a raw SSE message to a specific client. Used by the SSE controller for the
    /// initial connected event and heartbeats.
    /// </summary>
    public async Task WriteToClientAsync(string connectionId, string message)
    {
        if (_clients.TryGetValue(connectionId, out var client))
        {
            await WriteToClientInternalAsync(client, message);
        }
    }

    /// <summary>
    /// Writes to one client under its own write lock with a timeout. Any failure (cancelled,
    /// timed out, stream error) drops the client. Never throws.
    /// </summary>
    private async Task WriteToClientInternalAsync(SseClient client, string message)
    {
        if (client.CancellationToken.IsCancellationRequested)
        {
            RemoveClient(client.ConnectionId);
            return;
        }

        bool acquired = false;
        try
        {
            await client.WriteLock.WaitAsync(client.CancellationToken);
            acquired = true;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(client.CancellationToken);
            cts.CancelAfter(WriteTimeout);

            await client.Response.WriteAsync(message, cts.Token);
            await client.Response.Body.FlushAsync(cts.Token);
        }
        catch (Exception)
        {
            RemoveClient(client.ConnectionId);
        }
        finally
        {
            if (acquired)
            {
                client.WriteLock.Release();
            }
        }
    }

    public int ClientCount => _clients.Count;
}
