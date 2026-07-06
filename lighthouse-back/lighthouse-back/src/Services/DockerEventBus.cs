using System.Collections.Concurrent;
using Docker.DotNet.Models;

namespace Lighthouse.Services;

/// <inheritdoc cref="IDockerEventBus" />
public class DockerEventBus : IDockerEventBus
{
    private readonly ConcurrentDictionary<Guid, Func<Message, Task>> _subscribers = new();
    private readonly ILogger<DockerEventBus> _logger;

    public DockerEventBus(ILogger<DockerEventBus> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe(Func<Message, Task> handler)
    {
        Guid id = Guid.NewGuid();
        _subscribers[id] = handler;
        return new Subscription(this, id);
    }

    public async Task PublishAsync(Message message)
    {
        foreach (KeyValuePair<Guid, Func<Message, Task>> subscriber in _subscribers)
        {
            try
            {
                await subscriber.Value(message);
            }
            catch (Exception ex)
            {
                // Isolate a faulting subscriber so it cannot break delivery to the others.
                _logger.LogError(ex, "Docker event subscriber threw while handling action {Action}", message.Action);
            }
        }
    }

    private void Unsubscribe(Guid id) => _subscribers.TryRemove(id, out _);

    private sealed class Subscription : IDisposable
    {
        private readonly DockerEventBus _bus;
        private readonly Guid _id;
        private bool _disposed;

        public Subscription(DockerEventBus bus, Guid id)
        {
            _bus = bus;
            _id = id;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _bus.Unsubscribe(_id);
        }
    }
}
