using System.Threading.Channels;
using Lighthouse.Models;

namespace Lighthouse.Services;

/// <summary>
/// In-memory queue for audit entries. Producers (request handlers) enqueue without hitting
/// the database; a background writer drains and persists them, keeping the audit write off
/// the request hot path (and off the single SQLite writer during a request).
/// </summary>
public interface IAuditQueue
{
    /// <summary>Enqueues an audit entry. Non-blocking; never throws.</summary>
    void Enqueue(AuditLog entry);

    ChannelReader<AuditLog> Reader { get; }

    /// <summary>Signals that no more entries will be enqueued (used on shutdown).</summary>
    void Complete();
}

public class AuditQueue : IAuditQueue
{
    // Unbounded: audit volume is low and entries are small, so we prefer never dropping a
    // security-relevant entry over bounding memory. SingleReader — one background writer.
    private readonly Channel<AuditLog> _channel = Channel.CreateUnbounded<AuditLog>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(AuditLog entry) => _channel.Writer.TryWrite(entry);

    public ChannelReader<AuditLog> Reader => _channel.Reader;

    public void Complete() => _channel.Writer.TryComplete();
}
