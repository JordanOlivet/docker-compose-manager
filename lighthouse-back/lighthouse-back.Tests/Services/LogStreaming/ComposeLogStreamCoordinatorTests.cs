using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Docker.DotNet.Models;
using FluentAssertions;
using Lighthouse.DTOs;
using Lighthouse.Services;
using Lighthouse.Services.LogStreaming;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Lighthouse.Tests.Services.LogStreaming;

public class ComposeLogStreamCoordinatorTests
{
    private readonly Mock<IContainerLogService> _logService = new();
    private readonly DockerEventBus _bus = new(new NullLogger<DockerEventBus>());

    private ComposeLogStreamCoordinator Build() =>
        new(_logService.Object, _bus, new NullLogger<ComposeLogStreamCoordinator>());

    private static ContainerLogSource Source(string id, string service) =>
        new(id, id, "proj", service, Tty: false);

    private static LogEntryDto Entry(string id, string ts, string msg) =>
        new(ts, id, id, id, "stdout", msg);

    // Yields the given entries then stays open until cancelled (simulates a live follow).
    private static async IAsyncEnumerable<LogEntryDto> LiveStream(
        IEnumerable<LogEntryDto> entries,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (LogEntryDto e in entries)
        {
            yield return e;
        }
        await Task.Delay(Timeout.Infinite, ct);
    }

    // Yields the given entries then completes (simulates a container that stops).
    private static async IAsyncEnumerable<LogEntryDto> FiniteStream(IEnumerable<LogEntryDto> entries)
    {
        foreach (LogEntryDto e in entries)
        {
            await Task.Yield();
            yield return e;
        }
    }

    private void SetupContainer(string id, string service, Func<CancellationToken, IAsyncEnumerable<LogEntryDto>> stream)
    {
        _logService.Setup(l => l.GetLogSourceAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Source(id, service));
        _logService.Setup(l => l.StreamAsync(
                It.Is<ContainerLogSource>(s => s.Id == id), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((ContainerLogSource _, int _, string? _, CancellationToken ct) => stream(ct));
    }

    private static async Task<List<ILogStreamItem>> CollectAsync(
        ComposeLogStreamCoordinator coordinator,
        CancellationToken ct,
        ConcurrentQueue<ILogStreamItem> sink)
    {
        try
        {
            await foreach (ILogStreamItem item in coordinator.StreamProjectAsync("proj", 100, null, null, ct))
            {
                sink.Enqueue(item);
            }
        }
        catch (OperationCanceledException)
        {
            // stream cancelled by test
        }
        return sink.ToList();
    }

    private static Message StartEvent(string containerId, string project) => new()
    {
        Action = "start",
        Type = "container",
        Actor = new Actor { ID = containerId, Attributes = new Dictionary<string, string>
        {
            ["com.docker.compose.project"] = project
        } }
    };

    [Fact]
    public async Task FansInFromMultipleContainers()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a", "b" });
        SetupContainer("a", "web", ct => LiveStream(new[] { Entry("a", "2026-07-04T12:00:00.000000000Z", "a1") }, ct));
        SetupContainer("b", "db", ct => LiveStream(new[] { Entry("b", "2026-07-04T12:00:01.000000000Z", "b1") }, ct));

        var sink = new ConcurrentQueue<ILogStreamItem>();
        using var cts = new CancellationTokenSource();
        var task = CollectAsync(Build(), cts.Token, sink);

        await Task.Delay(900);
        cts.Cancel();
        var items = await task;

        var logs = items.OfType<LogEntryDto>().Select(e => e.Message).ToList();
        logs.Should().Contain("a1").And.Contain("b1");
    }

    [Fact]
    public async Task EmitsContainersSnapshotForAttachedContainers()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a" });
        SetupContainer("a", "web", ct => LiveStream(new[] { Entry("a", "2026-07-04T12:00:00.000000000Z", "a1") }, ct));

        var sink = new ConcurrentQueue<ILogStreamItem>();
        using var cts = new CancellationTokenSource();
        var task = CollectAsync(Build(), cts.Token, sink);

        await Task.Delay(900);
        cts.Cancel();
        var items = await task;

        items.OfType<ContainersSnapshot>().Should().NotBeEmpty();
        items.OfType<ContainersSnapshot>().Last().Containers.Should().Contain(c => c.Id == "a" && c.Service == "web");
    }

    [Fact]
    public async Task AttachesContainerStartedMidStream()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        SetupContainer("late", "worker", ct => LiveStream(new[] { Entry("late", "2026-07-04T12:00:05.000000000Z", "late1") }, ct));

        var sink = new ConcurrentQueue<ILogStreamItem>();
        using var cts = new CancellationTokenSource();
        var task = CollectAsync(Build(), cts.Token, sink);

        await Task.Delay(700); // let warmup pass
        await _bus.PublishAsync(StartEvent("late", "proj"));
        await Task.Delay(400);
        cts.Cancel();
        var items = await task;

        items.OfType<LogEntryDto>().Should().Contain(e => e.Message == "late1");
    }

    [Fact]
    public async Task IgnoresStartEventsFromOtherProjects()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        SetupContainer("other", "x", ct => LiveStream(new[] { Entry("other", "2026-07-04T12:00:05.000000000Z", "nope") }, ct));

        var sink = new ConcurrentQueue<ILogStreamItem>();
        using var cts = new CancellationTokenSource();
        var task = CollectAsync(Build(), cts.Token, sink);

        await Task.Delay(700);
        await _bus.PublishAsync(StartEvent("other", "different-project"));
        await Task.Delay(300);
        cts.Cancel();
        var items = await task;

        items.OfType<LogEntryDto>().Should().NotContain(e => e.Message == "nope");
        _logService.Verify(l => l.StreamAsync(It.Is<ContainerLogSource>(s => s.Id == "other"),
            It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetachesWhenContainerStreamCompletes()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "gone" });
        // Finite stream → the pump completes → the container detaches.
        SetupContainer("gone", "web", _ => FiniteStream(new[] { Entry("gone", "2026-07-04T12:00:00.000000000Z", "bye") }));

        var sink = new ConcurrentQueue<ILogStreamItem>();
        using var cts = new CancellationTokenSource();
        var task = CollectAsync(Build(), cts.Token, sink);

        await Task.Delay(900);
        cts.Cancel();
        var items = await task;

        // Last roster must no longer contain the finished container.
        items.OfType<ContainersSnapshot>().Last().Containers.Should().NotContain(c => c.Id == "gone");
    }
}
