using System.Globalization;
using Lighthouse.DTOs;
using Lighthouse.Services.LogStreaming;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lighthouse.Tests.Services;

/// <summary>
/// Unit tests for AppLogService: CLEF parsing, filtering, and history paging read from
/// on-disk rolling log files.
/// </summary>
public class AppLogServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppLogBroadcastSink _sink = new();

    public AppLogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "lighthouse-applog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private AppLogService CreateService()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:WriteTo:0:Name"] = "File",
                ["Serilog:WriteTo:0:Args:path"] = Path.Combine(_tempDir, "app-.clef")
            })
            .Build();

        return new AppLogService(config, _sink, NullLogger<AppLogService>.Instance);
    }

    private void WriteLogFile(string dateInfix, params string[] clefLines)
    {
        File.WriteAllLines(Path.Combine(_tempDir, $"app-{dateInfix}.clef"), clefLines);
    }

    private static string Clef(string timestamp, string? level, string message, string? sourceContext = null, string? username = null, string? exception = null)
    {
        var parts = new List<string> { $"\"@t\":\"{timestamp}\"" };
        if (level != null) parts.Add($"\"@l\":\"{level}\"");
        parts.Add($"\"@m\":\"{message}\"");
        if (exception != null) parts.Add($"\"@x\":\"{exception}\"");
        if (sourceContext != null) parts.Add($"\"SourceContext\":\"{sourceContext}\"");
        if (username != null) parts.Add($"\"Username\":\"{username}\"");
        return "{" + string.Join(",", parts) + "}";
    }

    [Fact]
    public void ParseClefLine_DefaultsLevelToInformation_WhenLevelOmitted()
    {
        AppLogEntryDto? entry = AppLogService.ParseClefLine(Clef("2026-07-06T10:00:00.0000000Z", null, "hello"));

        entry.Should().NotBeNull();
        entry!.Level.Should().Be("Information");
        entry.Message.Should().Be("hello");
    }

    [Fact]
    public void ParseClefLine_ExtractsAllFields()
    {
        AppLogEntryDto? entry = AppLogService.ParseClefLine(
            Clef("2026-07-06T10:00:00.0000000Z", "Warning", "boom", sourceContext: "Lighthouse.Foo", username: "alice", exception: "System.Exception: x"));

        entry.Should().NotBeNull();
        entry!.Level.Should().Be("Warning");
        entry.Category.Should().Be("Lighthouse.Foo");
        entry.Username.Should().Be("alice");
        entry.Exception.Should().Be("System.Exception: x");
    }

    [Fact]
    public void ParseClefLine_ReturnsNull_ForTornOrEmptyLine()
    {
        AppLogService.ParseClefLine("").Should().BeNull();
        AppLogService.ParseClefLine("{ this is not valid json").Should().BeNull();
        AppLogService.ParseClefLine("{\"@m\":\"no timestamp\"}").Should().BeNull();
    }

    [Fact]
    public void Matches_LevelFilter_IsCaseInsensitiveAndExact()
    {
        AppLogEntryDto entry = new("2026-07-06T10:00:00Z", "Warning", null, null, "m", null);

        AppLogService.Matches(entry, new AppLogFilter(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "warning" }, null, null, null)).Should().BeTrue();
        AppLogService.Matches(entry, new AppLogFilter(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Error" }, null, null, null)).Should().BeFalse();
        AppLogService.Matches(entry, new AppLogFilter(new HashSet<string>(), null, null, null)).Should().BeTrue("empty set means all levels");
    }

    [Fact]
    public void Matches_SearchScansMessageAndException()
    {
        AppLogEntryDto entry = new("2026-07-06T10:00:00Z", "Error", null, null, "connection failed", "SocketException: refused");

        AppLogService.Matches(entry, new AppLogFilter(null, null, null, "REFUSED")).Should().BeTrue();
        AppLogService.Matches(entry, new AppLogFilter(null, null, null, "connection")).Should().BeTrue();
        AppLogService.Matches(entry, new AppLogFilter(null, null, null, "timeout")).Should().BeFalse();
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEntriesAscending_AcrossRollingFiles()
    {
        // Older file (earlier date infix) and newer file.
        WriteLogFile("20260705",
            Clef("2026-07-05T09:00:00.0000000Z", "Information", "day1-a"),
            Clef("2026-07-05T09:00:01.0000000Z", "Information", "day1-b"));
        WriteLogFile("20260706",
            Clef("2026-07-06T10:00:00.0000000Z", "Information", "day2-a"));

        AppLogService service = CreateService();
        AppLogPageDto page = await service.GetHistoryAsync(new AppLogFilter(null, null, null, null), tail: 10, until: null, CancellationToken.None);

        page.Entries.Select(e => e.Message).Should().Equal("day1-a", "day1-b", "day2-a");
        page.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetHistoryAsync_RespectsTailAndReportsHasMore()
    {
        WriteLogFile("20260706",
            Clef("2026-07-06T10:00:00.0000000Z", "Information", "a"),
            Clef("2026-07-06T10:00:01.0000000Z", "Information", "b"),
            Clef("2026-07-06T10:00:02.0000000Z", "Information", "c"));

        AppLogService service = CreateService();
        AppLogPageDto page = await service.GetHistoryAsync(new AppLogFilter(null, null, null, null), tail: 2, until: null, CancellationToken.None);

        // Newest two, returned ascending.
        page.Entries.Select(e => e.Message).Should().Equal("b", "c");
        page.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GetHistoryAsync_UntilCursor_ExcludesNewerEntries()
    {
        WriteLogFile("20260706",
            Clef("2026-07-06T10:00:00.0000000Z", "Information", "a"),
            Clef("2026-07-06T10:00:01.0000000Z", "Information", "b"),
            Clef("2026-07-06T10:00:02.0000000Z", "Information", "c"));

        AppLogService service = CreateService();
        AppLogPageDto page = await service.GetHistoryAsync(
            new AppLogFilter(null, null, null, null), tail: 10, until: "2026-07-06T10:00:02.0000000Z", CancellationToken.None);

        page.Entries.Select(e => e.Message).Should().Equal("a", "b");
    }

    [Fact]
    public async Task GetHistoryAsync_AppliesLevelFilter()
    {
        WriteLogFile("20260706",
            Clef("2026-07-06T10:00:00.0000000Z", "Information", "info-line"),
            Clef("2026-07-06T10:00:01.0000000Z", "Error", "error-line"));

        AppLogService service = CreateService();
        AppLogPageDto page = await service.GetHistoryAsync(
            new AppLogFilter(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Error" }, null, null, null),
            tail: 10, until: null, CancellationToken.None);

        page.Entries.Select(e => e.Message).Should().Equal("error-line");
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmpty_WhenNoLogFiles()
    {
        AppLogService service = CreateService();
        AppLogPageDto page = await service.GetHistoryAsync(new AppLogFilter(null, null, null, null), tail: 10, until: null, CancellationToken.None);

        page.Entries.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task StreamAsync_EmitsHistoryThenLiveEvents()
    {
        WriteLogFile("20260706",
            Clef("2026-07-06T10:00:00.0000000Z", "Information", "historical"));

        AppLogService service = CreateService();
        using CancellationTokenSource cts = new();

        List<AppLogEntryDto> received = new();
        Task consume = Task.Run(async () =>
        {
            await foreach (ILogStreamItem item in service.StreamAsync(new AppLogFilter(null, null, null, null), tail: 10, cts.Token))
            {
                received.Add((AppLogEntryDto)item);
                if (received.Count >= 2) { cts.Cancel(); }
            }
        });

        // Give the consumer a moment to read history and subscribe, then emit a live event.
        await WaitUntilAsync(() => received.Count >= 1, TimeSpan.FromSeconds(2));
        _sink.Emit(MakeLiveEvent("live-event"));

        try { await consume; } catch (OperationCanceledException) { /* expected */ }

        received.Select(e => e.Message).Should().ContainInOrder("historical", "live-event");
    }

    private static Serilog.Events.LogEvent MakeLiveEvent(string message)
    {
        return new Serilog.Events.LogEvent(
            DateTimeOffset.UtcNow.AddMinutes(1),
            Serilog.Events.LogEventLevel.Information,
            exception: null,
            new Serilog.Parsing.MessageTemplateParser().Parse(message),
            Array.Empty<Serilog.Events.LogEventProperty>());
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}
