using docker_compose_manager_back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace docker_compose_manager_back.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class SseController : ControllerBase
{
    private readonly SseConnectionManagerService _sseManager;
    private readonly ILogger<SseController> _logger;

    public SseController(SseConnectionManagerService sseManager, ILogger<SseController> logger)
    {
        _sseManager = sseManager;
        _logger = logger;
    }

    /// <summary>
    /// SSE endpoint for real-time events (container state, compose project state, operation updates).
    /// Replaces SignalR OperationsHub.
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        // Only set Connection header for HTTP/1.1 (not valid for HTTP/2+)
        if (Request.Protocol == "HTTP/1.1")
        {
            Response.Headers.Connection = "keep-alive";
        }

        string connectionId = Guid.NewGuid().ToString();

        var client = new SseClient
        {
            Response = Response,
            ConnectionId = connectionId,
            CancellationToken = cancellationToken
        };

        _sseManager.AddClient(client);

        try
        {
            // Send initial connected event through the SSE manager to avoid concurrent
            // writes with BroadcastAsync (both target the same HttpResponse stream).
            await _sseManager.WriteToClientAsync(connectionId,
                $"event: connected\ndata: {{\"connectionId\":\"{connectionId}\"}}\n\n");

            // Keep connection alive with heartbeat (also coordinated via manager)
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                // Send heartbeat comment (not an event, just keeps connection alive)
                await _sseManager.WriteToClientAsync(connectionId, ": heartbeat\n\n");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SSE client disconnected: {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSE connection error for client: {ConnectionId}", connectionId);
        }
        finally
        {
            _sseManager.RemoveClient(connectionId);
        }
    }
}
