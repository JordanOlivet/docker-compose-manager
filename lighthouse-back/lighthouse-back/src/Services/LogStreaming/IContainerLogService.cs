using Lighthouse.DTOs;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Identity and log-relevant configuration of a container, resolved once per request.
/// </summary>
/// <param name="Id">Full container ID.</param>
/// <param name="Name">Container name without the leading '/'.</param>
/// <param name="Project">com.docker.compose.project label, null for standalone containers.</param>
/// <param name="Service">com.docker.compose.service label, null for standalone containers.</param>
/// <param name="Tty">Whether the container was created with a TTY (changes log stream framing).</param>
public record ContainerLogSource(string Id, string Name, string? Project, string? Service, bool Tty);

/// <summary>
/// Structured access to container logs: history pages (until-cursor pagination)
/// and follow streams, both emitting <see cref="LogEntryDto"/>.
/// </summary>
public interface IContainerLogService
{
    /// <summary>
    /// Inspects the container. Returns null when it does not exist.
    /// </summary>
    Task<ContainerLogSource?> GetLogSourceAsync(string containerId, CancellationToken ct = default);

    /// <summary>
    /// Fetches one page of historical logs, ascending by timestamp.
    /// <paramref name="until"/> is an RFC3339Nano cursor (exclusive-ish: Docker treats
    /// it inclusively, callers dedup the boundary); null means "up to now".
    /// </summary>
    Task<LogPageDto> GetHistoryAsync(ContainerLogSource source, int tail, string? until, CancellationToken ct = default);

    /// <summary>
    /// Follows the container's log output. When <paramref name="since"/> is set
    /// (RFC3339Nano, reconnect resume), <paramref name="tail"/> is ignored and all
    /// lines since that timestamp are replayed.
    /// </summary>
    IAsyncEnumerable<LogEntryDto> StreamAsync(ContainerLogSource source, int tail, string? since, CancellationToken ct = default);
}
