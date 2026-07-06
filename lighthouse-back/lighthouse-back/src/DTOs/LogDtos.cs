namespace Lighthouse.DTOs;

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
    string Message);

/// <summary>
/// One page of historical logs. Entries are sorted ascending by timestamp.
/// </summary>
public record LogPageDto(List<LogEntryDto> Entries, bool HasMore);
