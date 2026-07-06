using Docker.DotNet.Models;

namespace Lighthouse.Services;

/// <summary>
/// In-process fan-out of raw Docker container events. Lets multiple in-process
/// consumers (e.g. per-request compose log coordinators) react to container
/// start/stop without each opening its own Docker event stream.
/// </summary>
public interface IDockerEventBus
{
    /// <summary>
    /// Registers a handler invoked for every published container event.
    /// Dispose the returned token to unsubscribe.
    /// </summary>
    IDisposable Subscribe(Func<Message, Task> handler);

    /// <summary>
    /// Publishes an event to all current subscribers. Never throws; a faulting
    /// subscriber is isolated and does not affect the others.
    /// </summary>
    Task PublishAsync(Message message);
}
