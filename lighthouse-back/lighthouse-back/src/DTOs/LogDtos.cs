namespace Lighthouse.DTOs;

/// <summary>
/// Marker for anything that can be written to a log SSE stream, so log lines and
/// out-of-band control frames (e.g. the compose container roster) can flow through a
/// single ordered channel and a single writer — avoiding concurrent writes to the
/// same HTTP response.
/// </summary>
public interface ILogStreamItem { }

/// <summary>
/// A single structured log line from a container.
/// </summary>
/// <param name="Timestamp">
/// RFC3339Nano timestamp exactly as emitted by Docker (string to preserve nanosecond
/// precision — it doubles as the pagination cursor). Empty when the line had no
/// parseable timestamp prefix (e.g. TTY progress output); such entries must not be
/// used as cursors.
/// </param>
/// <param name="ContainerId">Full container ID.</param>
/// <param name="ContainerName">Container name without the leading '/'.</param>
/// <param name="Service">com.docker.compose.service label, null for standalone containers.</param>
/// <param name="Stream">"stdout" or "stderr".</param>
/// <param name="Message">Raw line content, ANSI codes preserved, timestamp prefix stripped.</param>
public record LogEntryDto(
    string Timestamp,
    string ContainerId,
    string ContainerName,
    string? Service,
    string Stream,
    string Message) : ILogStreamItem;

/// <summary>
/// One page of historical logs. Entries are sorted ascending by timestamp.
/// </summary>
public record LogPageDto(List<LogEntryDto> Entries, bool HasMore);

/// <summary>
/// A container currently attached to a compose log stream (drives the frontend's
/// per-container filter chips).
/// </summary>
public record AttachedContainerDto(string Id, string Name, string? Service, string State);

/// <summary>
/// Full roster of containers attached to a compose log stream, re-sent whenever a
/// container attaches or detaches.
/// </summary>
public record ContainersSnapshot(List<AttachedContainerDto> Containers) : ILogStreamItem;
