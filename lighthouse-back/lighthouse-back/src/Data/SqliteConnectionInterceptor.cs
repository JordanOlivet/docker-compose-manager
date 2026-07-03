using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lighthouse.Data;

/// <summary>
/// Applies per-connection SQLite PRAGMAs whenever a connection is opened:
/// <list type="bullet">
/// <item>WAL journal mode — readers no longer block the single writer, drastically
/// reducing "database is locked" errors under concurrent writes (SSE, audit, operations).</item>
/// <item>busy_timeout — a writer waits (instead of failing immediately) when the DB is
/// momentarily locked.</item>
/// <item>foreign_keys — enforce FK constraints (off by default in SQLite).</item>
/// </list>
/// busy_timeout is per-connection so it must be set on every open (WAL is persistent but
/// re-asserting it is harmless).
/// </summary>
public sealed class SqliteConnectionInterceptor : DbConnectionInterceptor
{
    private const string Pragmas =
        "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using DbCommand command = connection.CreateCommand();
        command.CommandText = Pragmas;
        command.ExecuteNonQuery();
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = Pragmas;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
