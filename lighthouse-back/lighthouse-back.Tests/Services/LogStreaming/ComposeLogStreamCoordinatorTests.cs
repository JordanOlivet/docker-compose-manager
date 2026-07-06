using System.Collections.Concurrent;
using System.Diagnostics;
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

    // Zero warmup keeps the tests deterministic — items flow straight through, no
    // wall-clock window to race against on a loaded CI runner.
    private ComposeLogStreamCoordinator Build() =>
        new(_logService.Object, _bus, new NullLogger<ComposeLogStreamCoordinator>(), TimeSpan.Zero);

    private static ContainerLogSource Source(string id, string service) =>
        new(id, id, "proj", service, Tty: false);

    private static LogEntryDto Entry(string id, string ts, string msg) =>
        new(ts, id, id, id, "stdout", msg);

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

    /// <summary>
    /// Runs the stream in the background, collecting items into a thread-safe queue.
    /// Cancel via the returned source and await the task to finish.
    /// </summary>
    private (Task Task, ConcurrentQueue<ILogStreamItem> Items, CancellationTokenSource Cts) StartCollecting(
        ComposeLogStreamCoordinator coordinator)
    {
        var items = new ConcurrentQueue<ILogStreamItem>();
        var cts = new CancellationTokenSource();
        Task task = Task.Run(async () =>
        {
            try
            {
                await foreach (ILogStreamItem item in coordinator.StreamProjectAsync("proj", 100, null, null, cts.Token))
                {
                    items.Enqueue(item);
                }
            }
            catch (OperationCanceledException)
            {
                // cancelled by the test
            }
        });
        return (task, items, cts);
    }

    private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!predicate() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(15);
        }
    }

    private static Message StartEvent(string containerId, string project) => new()
    {
        Action = "start",
        Type = "container",
        Actor = new Actor
        {
            ID = containerId,
            Attributes = new Dictionary<string, string> { ["com.docker.compose.project"] = project }
        }
    };

    [Fact]
    public async Task FansInFromMultipleContainers()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a", "b" });
        SetupContainer("a", "web", ct => LiveStream(new[] { Entry("a", "2026-07-04T12:00:00.000000000Z", "a1") }, ct));
        SetupContainer("b", "db", ct => LiveStream(new[] { Entry("b", "2026-07-04T12:00:01.000000000Z", "b1") }, ct));

        var (task, items, cts) = StartCollecting(Build());

        await WaitForAsync(() =>
        {
            var msgs = items.OfType<LogEntryDto>().Select(e => e.Message).ToList();
            return msgs.Contains("a1") && msgs.Contains("b1");
        });
        cts.Cancel();
        await task;

        var logs = items.OfType<LogEntryDto>().Select(e => e.Message).ToList();
        logs.Should().Contain("a1").And.Contain("b1");
    }

    [Fact]
    public async Task EmitsContainersSnapshotForAttachedContainers()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "a" });
        SetupContainer("a", "web", ct => LiveStream(new[] { Entry("a", "2026-07-04T12:00:00.000000000Z", "a1") }, ct));

        var (task, items, cts) = StartCollecting(Build());

        await WaitForAsync(() => items.OfType<ContainersSnapshot>().Any(s => s.Containers.Any(c => c.Id == "a")));
        cts.Cancel();
        await task;

        items.OfType<ContainersSnapshot>()
            .SelectMany(s => s.Containers)
            .Should().Contain(c => c.Id == "a" && c.Service == "web");
    }

    [Fact]
    public async Task AttachesContainerStartedMidStream()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        SetupContainer("late", "worker", ct => LiveStream(new[] { Entry("late", "2026-07-04T12:00:05.000000000Z", "late1") }, ct));

        var (task, items, cts) = StartCollecting(Build());

        // Ensure the stream has started (and thus subscribed to the bus) before publishing.
        await WaitForAsync(() => items.Count >= 0 && !task.IsCompleted);
        await Task.Delay(50);
        await _bus.PublishAsync(StartEvent("late", "proj"));

        await WaitForAsync(() => items.OfType<LogEntryDto>().Any(e => e.Message == "late1"));
        cts.Cancel();
        await task;

        items.OfType<LogEntryDto>().Should().Contain(e => e.Message == "late1");
    }

    [Fact]
    public async Task IgnoresStartEventsFromOtherProjects()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        SetupContainer("other", "x", ct => LiveStream(new[] { Entry("other", "2026-07-04T12:00:05.000000000Z", "nope") }, ct));

        var (task, items, cts) = StartCollecting(Build());

        await Task.Delay(50);
        await _bus.PublishAsync(StartEvent("other", "different-project"));
        await Task.Delay(150);
        cts.Cancel();
        await task;

        items.OfType<LogEntryDto>().Should().NotContain(e => e.Message == "nope");
        _logService.Verify(l => l.StreamAsync(It.Is<ContainerLogSource>(s => s.Id == "other"),
            It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetachesWhenContainerStreamCompletes()
    {
        _logService.Setup(l => l.ListProjectContainerIdsAsync("proj", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "gone" });
        // Finite stream → the pump completes → the container detaches → empty roster.
        SetupContainer("gone", "web", _ => FiniteStream(new[] { Entry("gone", "2026-07-04T12:00:00.000000000Z", "bye") }));

        var (task, items, cts) = StartCollecting(Build());

        // Wait until a snapshot with an empty roster (the detach) has been emitted.
        await WaitForAsync(() =>
            items.OfType<ContainersSnapshot>().Any(s => s.Containers.Count == 0) &&
            items.OfType<ContainersSnapshot>().Any(s => s.Containers.Any(c => c.Id == "gone")));
        cts.Cancel();
        await task;

        var snapshots = items.OfType<ContainersSnapshot>().ToList();
        snapshots.Should().Contain(s => s.Containers.Any(c => c.Id == "gone")); // attached first
        snapshots.Last().Containers.Should().NotContain(c => c.Id == "gone");   // then detached
    }
}
