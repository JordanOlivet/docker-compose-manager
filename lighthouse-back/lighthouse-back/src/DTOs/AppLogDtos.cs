namespace Lighthouse.DTOs;

/// <summary>
/// A single application log event, parsed from a CLEF (compact JSON) log file or
/// converted from a live Serilog event.
/// </summary>
/// <param name="Timestamp">ISO-8601 UTC timestamp ("o" format) — doubles as the pagination cursor.</param>
/// <param name="Level">Serilog level name: Verbose, Debug, Information, Warning, Error, Fatal.</param>
/// <param name="Category">Source context (logger category), e.g. "Lighthouse.Services.ComposeService".</param>
/// <param name="Username">Authenticated user attached via LogContext enrichment, null for system events.</param>
/// <param name="Message">Rendered log message.</param>
/// <param name="Exception">Full exception text when the event carries one.</param>
public record AppLogEntryDto(
    string Timestamp,
    string Level,
    string? Category,
    string? Username,
    string Message,
    string? Exception) : ILogStreamItem;

/// <summary>
/// One page of historical application logs. Entries are sorted ascending by timestamp.
/// </summary>
/// <param name="Entries">Log entries, oldest first.</param>
/// <param name="HasMore">True when older entries matching the filter exist.</param>
public record AppLogPageDto(List<AppLogEntryDto> Entries, bool HasMore);

/// <summary>
/// Server-side filter for application logs. All criteria are combined with AND;
/// null/empty values are ignored.
/// </summary>
/// <param name="Levels">Level names to include (case-insensitive); empty = all levels.</param>
/// <param name="Category">Case-insensitive substring match on the source context.</param>
/// <param name="Username">Case-insensitive exact match on the enriched username.</param>
/// <param name="Search">Case-insensitive substring match on message + exception.</param>
public record AppLogFilter(
    HashSet<string>? Levels,
    string? Category,
    string? Username,
    string? Search);
