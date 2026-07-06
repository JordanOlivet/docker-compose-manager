using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Docker.DotNet.Models;
using Lighthouse.DTOs;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Fans in the live log streams of every container in a compose project into a single
/// ordered sequence, and keeps the set of attached containers current as they start and
/// stop. One instance per SSE request.
/// </summary>
public class ComposeLogStreamCoordinator
{
    private static readonly TimeSpan DefaultWarmupWindow = TimeSpan.FromMilliseconds(500);
    private const int ChannelCapacity = 10_000;

    private readonly IContainerLogService _logService;
    private readonly IDockerEventBus _eventBus;
    private readonly ILogger<ComposeLogStreamCoordinator> _logger;
    private readonly TimeSpan _warmupWindow;

    public ComposeLogStreamCoordinator(
        IContainerLogService logService,
        IDockerEventBus eventBus,
        ILogger<ComposeLogStreamCoordinator> logger,
        TimeSpan? warmupWindow = null)
    {
        _logService = logService;
        _eventBus = eventBus;
        _logger = logger;
        _warmupWindow = warmupWindow ?? DefaultWarmupWindow;
    }

    /// <summary>
    /// Streams the project's logs as <see cref="LogEntryDto"/> items interleaved with
    /// <see cref="ContainersSnapshot"/> control frames (emitted on every attach/detach).
    /// Optionally restricts to the given services. When <paramref name="since"/> is set,
    /// each container resumes from that timestamp instead of replaying its tail.
    /// </summary>
    public async IAsyncEnumerable<ILogStreamItem> StreamProjectAsync(
        string projectName,
        int tailPerContainer,
        string? since,
        IReadOnlySet<string>? services,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Channel<ILogStreamItem> channel = Channel.CreateBounded<ILogStreamItem>(
            new BoundedChannelOptions(ChannelCapacity) { FullMode = BoundedChannelFullMode.Wait });

        var attached = new ConcurrentDictionary<string, AttachedContainerDto>();
        var pumping = new ConcurrentDictionary<string, byte>();

        void EmitSnapshot()
        {
            var roster = attached.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
            channel.Writer.TryWrite(new ContainersSnapshot(roster));
        }

        bool ServiceAllowed(string? service) =>
            services == null || services.Count == 0 || (service != null && services.Contains(service));

        async Task AttachAsync(string containerId, int tail, string? pumpSince)
        {
            ContainerLogSource? source;
            try
            {
                source = await _logService.GetLogSourceAsync(containerId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve log source for container {ContainerId}", containerId);
                return;
            }

            if (source == null || !ServiceAllowed(source.Service))
            {
                return;
            }
            if (!pumping.TryAdd(source.Id, 0))
            {
                return; // already streaming this container
            }

            attached[source.Id] = new AttachedContainerDto(source.Id, source.Name, source.Service, "running");
            EmitSnapshot();

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (LogEntryDto entry in _logService.StreamAsync(source, tail, pumpSince, ct))
                    {
                        await channel.Writer.WriteAsync(entry, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    // request ended
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Log pump for container {ContainerId} ended with error", source.Id);
                }
                finally
                {
                    pumping.TryRemove(source.Id, out _);
                    attached.TryRemove(source.Id, out _);
                    EmitSnapshot();
                }
            }, ct);
        }

        // Attach a container that starts mid-stream (resume from now, no tail replay).
        using IDisposable subscription = _eventBus.Subscribe(async message =>
        {
            if (message.Action != "start" || message.Actor?.Attributes == null)
            {
                return;
            }
            if (!message.Actor.Attributes.TryGetValue("com.docker.compose.project", out string? project) ||
                !string.Equals(project, projectName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            await AttachAsync(message.Actor.ID, tail: 0, pumpSince: Rfc3339Now());
        });

        // Attach the containers already running when the stream opens.
        IReadOnlyList<string> running = await _logService.ListProjectContainerIdsAsync(projectName, includeStopped: false, ct);
        foreach (string containerId in running)
        {
            await AttachAsync(containerId, tailPerContainer, since);
        }

        await foreach (ILogStreamItem item in ReadWithWarmupAsync(channel.Reader, _warmupWindow, ct))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Reorders the initial burst (the per-container tail bursts arrive interleaved)
    /// within a short window, then passes everything through live. Log items are sorted
    /// by timestamp; container snapshots keep their arrival order.
    /// </summary>
    private static async IAsyncEnumerable<ILogStreamItem> ReadWithWarmupAsync(
        ChannelReader<ILogStreamItem> reader,
        TimeSpan warmupWindow,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var warmup = new List<ILogStreamItem>();
        using (var warmupCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            warmupCts.CancelAfter(warmupWindow);
            try
            {
                await foreach (ILogStreamItem item in reader.ReadAllAsync(warmupCts.Token))
                {
                    warmup.Add(item);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // warmup window elapsed
            }
        }

        // Stable sort: container snapshots (empty key) lead, then log entries by
        // timestamp; OrderBy preserves arrival order for equal keys.
        IEnumerable<ILogStreamItem> ordered = warmup.OrderBy(
            item => item is LogEntryDto entry ? entry.Timestamp : string.Empty,
            StringComparer.Ordinal);

        foreach (ILogStreamItem item in ordered)
        {
            yield return item;
        }

        await foreach (ILogStreamItem item in reader.ReadAllAsync(ct))
        {
            yield return item;
        }
    }

    private static string Rfc3339Now() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'");
}
