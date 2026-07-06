using System.Threading.Channels;
using Serilog.Core;
using Serilog.Events;

namespace Lighthouse.Services.LogStreaming;

/// <summary>
/// Serilog sink that fans out every log event to in-process subscribers, powering
/// the application-logs SSE live tail. Subscriber channels are bounded and drop the
/// oldest events when a slow client falls behind — the logging pipeline must never
/// block on a subscriber.
/// </summary>
public sealed class AppLogBroadcastSink : ILogEventSink
{
    private const int SubscriberChannelCapacity = 2_000;

    private readonly object _lock = new();
    private readonly List<Channel<LogEvent>> _subscribers = [];

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            foreach (Channel<LogEvent> subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(logEvent);
            }
        }
    }

    /// <summary>
    /// Subscribes to the live log event stream. Dispose the returned subscription
    /// to detach; the reader completes when the subscription is disposed.
    /// </summary>
    public Subscription Subscribe()
    {
        Channel<LogEvent> channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(SubscriberChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        return new Subscription(this, channel);
    }

    private void Unsubscribe(Channel<LogEvent> channel)
    {
        lock (_lock)
        {
            _subscribers.Remove(channel);
        }
        channel.Writer.TryComplete();
    }

    public sealed class Subscription : IDisposable
    {
        private readonly AppLogBroadcastSink _sink;
        private readonly Channel<LogEvent> _channel;

        internal Subscription(AppLogBroadcastSink sink, Channel<LogEvent> channel)
        {
            _sink = sink;
            _channel = channel;
        }

        public ChannelReader<LogEvent> Reader => _channel.Reader;

        public void Dispose() => _sink.Unsubscribe(_channel);
    }
}
