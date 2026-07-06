using Docker.DotNet.Models;
using FluentAssertions;
using Lighthouse.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lighthouse.Tests.Services;

public class DockerEventBusTests
{
    private static Message Msg(string action) => new() { Action = action, Type = "container" };

    [Fact]
    public async Task Publish_InvokesAllSubscribers()
    {
        var bus = new DockerEventBus(new NullLogger<DockerEventBus>());
        int a = 0, b = 0;
        bus.Subscribe(_ => { a++; return Task.CompletedTask; });
        bus.Subscribe(_ => { b++; return Task.CompletedTask; });

        await bus.PublishAsync(Msg("start"));

        a.Should().Be(1);
        b.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_UnsubscribesHandler()
    {
        var bus = new DockerEventBus(new NullLogger<DockerEventBus>());
        int count = 0;
        IDisposable sub = bus.Subscribe(_ => { count++; return Task.CompletedTask; });

        await bus.PublishAsync(Msg("start"));
        sub.Dispose();
        await bus.PublishAsync(Msg("stop"));

        count.Should().Be(1);
    }

    [Fact]
    public async Task Publish_FaultingSubscriber_DoesNotAffectOthers()
    {
        var bus = new DockerEventBus(new NullLogger<DockerEventBus>());
        bus.Subscribe(_ => throw new InvalidOperationException("boom"));
        int reached = 0;
        bus.Subscribe(_ => { reached++; return Task.CompletedTask; });

        Func<Task> act = () => bus.PublishAsync(Msg("start"));

        await act.Should().NotThrowAsync();
        reached.Should().Be(1);
    }

    [Fact]
    public async Task Publish_NoSubscribers_DoesNotThrow()
    {
        var bus = new DockerEventBus(new NullLogger<DockerEventBus>());

        Func<Task> act = () => bus.PublishAsync(Msg("start"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void DoubleDispose_IsSafe()
    {
        var bus = new DockerEventBus(new NullLogger<DockerEventBus>());
        IDisposable sub = bus.Subscribe(_ => Task.CompletedTask);

        sub.Dispose();
        Action act = () => sub.Dispose();

        act.Should().NotThrow();
    }
}
