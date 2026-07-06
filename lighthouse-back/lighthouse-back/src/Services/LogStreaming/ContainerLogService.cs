using System.Runtime.CompilerServices;
using Docker.DotNet;
using Docker.DotNet.Models;
using Lighthouse.DTOs;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Reads container logs through the Docker Engine API with correct multiplexed-stream
/// framing (see <see cref="LogLineBuffer"/>) and per-line RFC3339Nano timestamps.
/// </summary>
public class ContainerLogService : IContainerLogService, IDisposable
{
    private const string ComposeProjectLabel = "com.docker.compose.project";
    private const string ComposeServiceLabel = "com.docker.compose.service";
    private const int ReadBufferSize = 8192;

    private readonly DockerClient _dockerClient;
    private readonly ILogger<ContainerLogService> _logger;

    public ContainerLogService(IConfiguration configuration, ILogger<ContainerLogService> logger)
    {
        _logger = logger;

        string? dockerHost = configuration["Docker:Host"];
        if (string.IsNullOrEmpty(dockerHost))
        {
            throw new ArgumentException("Docker host is not configured. Set 'Docker__Host' environment variable.");
        }

        _dockerClient = new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
    }

    public async Task<ContainerLogSource?> GetLogSourceAsync(string containerId, CancellationToken ct = default)
    {
        try
        {
            ContainerInspectResponse inspect = await _dockerClient.Containers.InspectContainerAsync(containerId, ct);

            IDictionary<string, string>? labels = inspect.Config?.Labels;
            string? project = null;
            string? service = null;
            labels?.TryGetValue(ComposeProjectLabel, out project);
            labels?.TryGetValue(ComposeServiceLabel, out service);

            return new ContainerLogSource(
                Id: inspect.ID,
                Name: inspect.Name?.TrimStart('/') ?? "unknown",
                Project: project,
                Service: service,
                Tty: inspect.Config?.Tty ?? false);
        }
        catch (DockerContainerNotFoundException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListProjectContainerIdsAsync(string projectName, bool includeStopped, CancellationToken ct = default)
    {
        var parameters = new ContainersListParameters
        {
            All = includeStopped,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool> { [$"{ComposeProjectLabel}={projectName}"] = true }
            }
        };

        IList<ContainerListResponse> containers = await _dockerClient.Containers.ListContainersAsync(parameters, ct);
        return containers.Select(c => c.ID).ToList();
    }

    public async Task<LogPageDto> GetHistoryAsync(ContainerLogSource source, int tail, string? until, CancellationToken ct = default)
    {
        ContainerLogsParameters parameters = new()
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = true,
            Tail = tail.ToString(),
            Until = until != null ? LogTimestampUtil.ToUnixNano(until) : null
        };

        List<LogEntryDto> entries = new();
        using MultiplexedStream stream = await _dockerClient.Containers.GetContainerLogsAsync(
            source.Id, source.Tty, parameters, ct);

        LogLineBuffer buffer = new();
        byte[] readBuffer = new byte[ReadBufferSize];

        while (true)
        {
            MultiplexedStream.ReadResult result = await stream.ReadOutputAsync(readBuffer, 0, readBuffer.Length, ct);
            if (result.Count == 0)
            {
                break;
            }

            buffer.Feed(result.Target, readBuffer.AsSpan(0, result.Count));
            foreach ((string line, string streamName) in buffer.DrainCompletedLines())
            {
                entries.Add(ToEntry(source, line, streamName));
            }
        }

        buffer.Flush();
        foreach ((string line, string streamName) in buffer.DrainCompletedLines())
        {
            entries.Add(ToEntry(source, line, streamName));
        }

        return new LogPageDto(entries, HasMore: entries.Count >= tail);
    }

    public async IAsyncEnumerable<LogEntryDto> StreamAsync(
        ContainerLogSource source,
        int tail,
        string? since,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ContainerLogsParameters parameters = new()
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = true,
            Follow = true,
            Tail = since != null ? "all" : tail.ToString(),
            Since = since != null ? LogTimestampUtil.ToUnixNano(since) : null
        };

        using MultiplexedStream stream = await _dockerClient.Containers.GetContainerLogsAsync(
            source.Id, source.Tty, parameters, ct);

        LogLineBuffer buffer = new();
        byte[] readBuffer = new byte[ReadBufferSize];

        while (!ct.IsCancellationRequested)
        {
            MultiplexedStream.ReadResult result = await stream.ReadOutputAsync(readBuffer, 0, readBuffer.Length, ct);
            if (result.Count == 0)
            {
                break;
            }

            buffer.Feed(result.Target, readBuffer.AsSpan(0, result.Count));
            foreach ((string line, string streamName) in buffer.DrainCompletedLines())
            {
                yield return ToEntry(source, line, streamName);
            }
        }

        buffer.Flush();
        foreach ((string line, string streamName) in buffer.DrainCompletedLines())
        {
            yield return ToEntry(source, line, streamName);
        }
    }

    private static LogEntryDto ToEntry(ContainerLogSource source, string line, string streamName)
    {
        LogTimestampUtil.TrySplitTimestampPrefix(line, out string timestamp, out string message);
        return new LogEntryDto(timestamp, source.Id, source.Name, source.Service, streamName, message);
    }

    public void Dispose()
    {
        _dockerClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
