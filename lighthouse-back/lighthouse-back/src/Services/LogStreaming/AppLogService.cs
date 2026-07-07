using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Lighthouse.DTOs;
using Serilog.Events;

namespace Lighthouse.Services.LogStreaming;

public interface IAppLogService
{
    /// <summary>
    /// Reads a page of historical application logs from the CLEF files, newest first,
    /// returned ascending. <paramref name="until"/> is an exclusive upper-bound cursor.
    /// </summary>
    Task<AppLogPageDto> GetHistoryAsync(AppLogFilter filter, int tail, string? until, CancellationToken ct);

    /// <summary>
    /// Streams application logs: last <paramref name="tail"/> matching lines from the
    /// files, then follows live events from the in-process broadcast sink.
    /// </summary>
    IAsyncEnumerable<ILogStreamItem> StreamAsync(AppLogFilter filter, int tail, CancellationToken ct);
}

/// <summary>
/// Reads application logs written by the Serilog file sink in CLEF
/// (RenderedCompactJsonFormatter) format and exposes a live tail through
/// <see cref="AppLogBroadcastSink"/>. The file location is derived from the
/// "Serilog:WriteTo" File sink configuration; rolling files share the configured
/// path with a date infix (e.g. app-20260706.clef).
/// </summary>
public class AppLogService : IAppLogService
{
    private readonly AppLogBroadcastSink _broadcastSink;
    private readonly ILogger<AppLogService> _logger;
    private readonly string? _logDirectory;
    private readonly string _filePrefix;
    private readonly string _fileExtension;

    public AppLogService(IConfiguration configuration, AppLogBroadcastSink broadcastSink, ILogger<AppLogService> logger)
    {
        _broadcastSink = broadcastSink;
        _logger = logger;

        string? configuredPath = FindFileSinkPath(configuration);
        if (configuredPath != null)
        {
            _logDirectory = Path.GetDirectoryName(configuredPath);
            _filePrefix = Path.GetFileNameWithoutExtension(configuredPath).TrimEnd('-') is { Length: > 0 } stem
                ? stem + "-"
                : Path.GetFileNameWithoutExtension(configuredPath);
            _fileExtension = Path.GetExtension(configuredPath);
        }
        else
        {
            _filePrefix = "app-";
            _fileExtension = ".clef";
        }
    }

    private static string? FindFileSinkPath(IConfiguration configuration)
    {
        foreach (IConfigurationSection sink in configuration.GetSection("Serilog:WriteTo").GetChildren())
        {
            if (sink.GetValue<string>("Name") == "File")
            {
                return sink.GetSection("Args").GetValue<string>("path");
            }
        }
        return null;
    }

    /// <summary>Rolling log files, newest first (rolling date infix sorts lexicographically).</summary>
    private List<string> GetLogFilesNewestFirst()
    {
        if (_logDirectory == null || !Directory.Exists(_logDirectory))
        {
            return [];
        }

        return Directory.GetFiles(_logDirectory, $"{_filePrefix}*{_fileExtension}")
            .Where(f => Path.GetFileName(f).StartsWith(_filePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AppLogPageDto> GetHistoryAsync(AppLogFilter filter, int tail, string? until, CancellationToken ct)
    {
        DateTimeOffset? untilTs = until != null
            ? DateTimeOffset.Parse(until, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

        List<AppLogEntryDto> collected = new(tail + 1);

        await Task.Run(() =>
        {
            foreach (string file in GetLogFilesNewestFirst())
            {
                ct.ThrowIfCancellationRequested();

                List<AppLogEntryDto> fileEntries = ReadMatchingEntries(file, filter, untilTs);
                // Within a file entries are chronological; walk backwards to fill the page
                // from the newest end.
                for (int i = fileEntries.Count - 1; i >= 0 && collected.Count <= tail; i--)
                {
                    collected.Add(fileEntries[i]);
                }

                if (collected.Count > tail)
                {
                    break;
                }
            }
        }, ct);

        bool hasMore = collected.Count > tail;
        if (hasMore)
        {
            collected.RemoveAt(collected.Count - 1);
        }

        collected.Reverse(); // ascending
        return new AppLogPageDto(collected, hasMore);
    }

    public async IAsyncEnumerable<ILogStreamItem> StreamAsync(
        AppLogFilter filter, int tail, [EnumeratorCancellation] CancellationToken ct)
    {
        // Subscribe before reading history so no event is lost in between; duplicates
        // from the overlap window are dropped by the timestamp check below.
        using AppLogBroadcastSink.Subscription subscription = _broadcastSink.Subscribe();

        AppLogPageDto history = await GetHistoryAsync(filter, tail, until: null, ct);
        DateTimeOffset lastEmitted = DateTimeOffset.MinValue;

        foreach (AppLogEntryDto entry in history.Entries)
        {
            lastEmitted = DateTimeOffset.Parse(entry.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            yield return entry;
        }

        await foreach (LogEvent logEvent in subscription.Reader.ReadAllAsync(ct))
        {
            if (logEvent.Timestamp <= lastEmitted)
            {
                continue;
            }

            AppLogEntryDto entry = ToDto(logEvent);
            if (Matches(entry, filter))
            {
                yield return entry;
            }
        }
    }

    private List<AppLogEntryDto> ReadMatchingEntries(string file, AppLogFilter filter, DateTimeOffset? untilTs)
    {
        List<AppLogEntryDto> entries = [];
        try
        {
            // Share-friendly read: Serilog keeps the current file open for writing.
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using StreamReader reader = new(stream);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                AppLogEntryDto? entry = ParseClefLine(line);
                if (entry == null || !Matches(entry, filter))
                {
                    continue;
                }

                if (untilTs.HasValue &&
                    DateTimeOffset.Parse(entry.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) >= untilTs.Value)
                {
                    continue;
                }

                entries.Add(entry);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read log file {LogFile}", file);
        }
        return entries;
    }

    public static AppLogEntryDto? ParseClefLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("@t", out JsonElement t))
            {
                return null;
            }

            string timestamp = t.GetString() ?? string.Empty;
            // CLEF omits @l for Information-level events.
            string level = root.TryGetProperty("@l", out JsonElement l) ? l.GetString() ?? "Information" : "Information";
            string message = root.TryGetProperty("@m", out JsonElement m) ? m.GetString() ?? string.Empty : string.Empty;
            string? exception = root.TryGetProperty("@x", out JsonElement x) ? x.GetString() : null;
            string? category = root.TryGetProperty("SourceContext", out JsonElement sc) ? sc.GetString() : null;
            string? username = root.TryGetProperty("Username", out JsonElement u) && u.ValueKind == JsonValueKind.String
                ? u.GetString()
                : null;

            return new AppLogEntryDto(timestamp, level, category, username, message, exception);
        }
        catch (JsonException)
        {
            // Torn/corrupt line (e.g. crash mid-write) — skip it.
            return null;
        }
    }

    public static AppLogEntryDto ToDto(LogEvent logEvent)
    {
        return new AppLogEntryDto(
            Timestamp: logEvent.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Level: logEvent.Level switch
            {
                LogEventLevel.Verbose => "Verbose",
                LogEventLevel.Debug => "Debug",
                LogEventLevel.Information => "Information",
                LogEventLevel.Warning => "Warning",
                LogEventLevel.Error => "Error",
                LogEventLevel.Fatal => "Fatal",
                _ => logEvent.Level.ToString()
            },
            Category: ScalarString(logEvent, "SourceContext"),
            Username: ScalarString(logEvent, "Username"),
            Message: logEvent.RenderMessage(CultureInfo.InvariantCulture),
            Exception: logEvent.Exception?.ToString());
    }

    private static string? ScalarString(LogEvent logEvent, string propertyName)
    {
        return logEvent.Properties.TryGetValue(propertyName, out LogEventPropertyValue? value)
            && value is ScalarValue { Value: string s }
            ? s
            : null;
    }

    public static bool Matches(AppLogEntryDto entry, AppLogFilter filter)
    {
        if (filter.Levels is { Count: > 0 } && !filter.Levels.Contains(entry.Level))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Category) &&
            (entry.Category == null || !entry.Category.Contains(filter.Category, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Username) &&
            !string.Equals(entry.Username, filter.Username, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Search) &&
            !entry.Message.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) &&
            (entry.Exception == null || !entry.Exception.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
