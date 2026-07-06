using System.Text.Json;
using System.Threading.Channels;
using Lighthouse.DTOs;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Writes a stream of log entries to an HTTP response as Server-Sent Events.
/// Entries are batched (flushed every 150ms or 256 entries) into `event: logs`
/// frames with a JSON payload; a `: heartbeat` comment is written every 30s of
/// inactivity. The bounded channel applies backpressure to the producer when the
/// client reads slowly. Entries are written in producer order — the producer is
/// responsible for chronological ordering.
/// </summary>
public static class SseLogStreamWriter
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private const int MaxBatchSize = 256;
    private const int ChannelCapacity = 10_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(
        HttpContext httpContext,
        IAsyncEnumerable<LogEntryDto> entries,
        ILogger logger,
        CancellationToken ct)
    {
        HttpResponse response = httpContext.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";

        // Only set Connection header for HTTP/1.1 (not valid for HTTP/2+)
        if (httpContext.Request.Protocol == "HTTP/1.1")
        {
            response.Headers.Connection = "keep-alive";
        }

        try
        {
            await WriteEventAsync(response, "connected",
                JsonSerializer.Serialize(new { streamId = Guid.NewGuid().ToString() }, JsonOptions), ct);

            Channel<LogEntryDto> channel = Channel.CreateBounded<LogEntryDto>(new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });

            Task producer = PumpAsync(entries, channel.Writer, ct);
            await ConsumeAsync(response, channel.Reader, ct);
            await producer;
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal behavior
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing SSE log stream");
            try
            {
                await WriteEventAsync(response, "error",
                    JsonSerializer.Serialize(new { message = "Failed to stream logs", code = "LOG_STREAM_FAILED" }, JsonOptions),
                    CancellationToken.None);
            }
            catch
            {
                // Client already disconnected
            }
        }
    }

    private static async Task PumpAsync(
        IAsyncEnumerable<LogEntryDto> entries,
        ChannelWriter<LogEntryDto> writer,
        CancellationToken ct)
    {
        try
        {
            await foreach (LogEntryDto entry in entries.WithCancellation(ct))
            {
                await writer.WriteAsync(entry, ct);
            }
            writer.Complete();
        }
        catch (Exception ex)
        {
            writer.Complete(ex);
        }
    }

    private static async Task ConsumeAsync(
        HttpResponse response,
        ChannelReader<LogEntryDto> reader,
        CancellationToken ct)
    {
        List<LogEntryDto> batch = new(MaxBatchSize);
        DateTime lastWrite = DateTime.UtcNow;
        DateTime? batchDeadline = null;

        while (true)
        {
            TimeSpan wait = batchDeadline.HasValue
                ? batchDeadline.Value - DateTime.UtcNow
                : HeartbeatInterval - (DateTime.UtcNow - lastWrite);
            if (wait < TimeSpan.Zero)
            {
                wait = TimeSpan.Zero;
            }

            bool completed = false;
            bool timedOut = false;
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(wait);
                try
                {
                    completed = !await reader.WaitToReadAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    timedOut = true;
                }
            }

            if (!completed && !timedOut)
            {
                while (batch.Count < MaxBatchSize && reader.TryRead(out LogEntryDto? entry))
                {
                    batch.Add(entry);
                }
                batchDeadline ??= DateTime.UtcNow + FlushInterval;

                if (batch.Count < MaxBatchSize)
                {
                    continue; // accumulate until the flush deadline or a full batch
                }
            }

            if (batch.Count > 0)
            {
                await WriteEventAsync(response, "logs",
                    JsonSerializer.Serialize(new { entries = batch }, JsonOptions), ct);
                batch.Clear();
                batchDeadline = null;
                lastWrite = DateTime.UtcNow;
            }
            else if (timedOut)
            {
                await response.WriteAsync(": heartbeat\n\n", ct);
                await response.Body.FlushAsync(ct);
                lastWrite = DateTime.UtcNow;
            }

            if (completed)
            {
                return;
            }
        }
    }

    private static async Task WriteEventAsync(HttpResponse response, string eventName, string jsonData, CancellationToken ct)
    {
        await response.WriteAsync($"event: {eventName}\ndata: {jsonData}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
