using Lighthouse.Data;
using Lighthouse.Models;
using Lighthouse.Services;

namespace Lighthouse.BackgroundServices;

/// <summary>
/// Drains the audit queue and persists entries to the database in batches, off the request
/// path. On shutdown it flushes whatever is still buffered so entries are not lost.
/// </summary>
public class AuditWriterBackgroundService : BackgroundService
{
    private const int MaxBatchSize = 100;

    private readonly IAuditQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditWriterBackgroundService> _logger;

    public AuditWriterBackgroundService(
        IAuditQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditWriterBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit writer background service started");

        try
        {
            await foreach (AuditLog first in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                // Coalesce whatever is already buffered into a single batch insert.
                var batch = new List<AuditLog> { first };
                while (batch.Count < MaxBatchSize && _queue.Reader.TryRead(out AuditLog? more))
                {
                    batch.Add(more);
                }

                await PersistBatchAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        // Final flush: persist anything still buffered without a cancelled token.
        var remaining = new List<AuditLog>();
        while (_queue.Reader.TryRead(out AuditLog? entry))
        {
            remaining.Add(entry);
        }
        if (remaining.Count > 0)
        {
            await PersistBatchAsync(remaining, CancellationToken.None);
        }

        _logger.LogInformation("Audit writer background service stopped");
    }

    private async Task PersistBatchAsync(List<AuditLog> batch, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditLogs.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit is best-effort: log and drop the batch rather than crash the writer.
            _logger.LogError(ex, "Failed to persist {Count} audit entries", batch.Count);
        }
    }
}
