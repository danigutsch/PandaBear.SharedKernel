using Npgsql;

namespace PandaBear.Npgsql;

/// <summary>
/// Acquires PostgreSQL advisory locks that PostgreSQL releases when the supplied transaction completes.
/// </summary>
public static class PostgreSqlTransactionAdvisoryLock
{
    /// <summary>Acquires an advisory lock using a typed numeric identity.</summary>
    /// <param name="connection">The open connection owning the transaction.</param>
    /// <param name="transaction">The active lock-owning transaction.</param>
    /// <param name="lockKey">The exact advisory lock identity.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The existing provider operation or lock lease.</returns>
    public static ValueTask AcquireKey(NpgsqlConnection connection, NpgsqlTransaction transaction, PostgreSqlLockKey lockKey, CancellationToken ct) =>
        Acquire(connection, transaction, lockKey.Value, ct);

    private const string AcquireSql = "SELECT pg_advisory_xact_lock(@lockKey);";

    /// <summary>
    /// Acquires an exclusive transaction-scoped advisory lock for <paramref name="lockKey" />.
    /// </summary>
    /// <param name="connection">The open PostgreSQL connection that owns <paramref name="transaction" />.</param>
    /// <param name="transaction">The active PostgreSQL transaction that owns the advisory lock.</param>
    /// <param name="lockKey">The caller-defined 64-bit advisory lock key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when PostgreSQL grants the lock.</returns>
    /// <remarks>
    /// PostgreSQL releases this lock when <paramref name="transaction" /> commits, rolls back, or is disposed.
    /// Callers own lock-key derivation and must retain the transaction through their critical section.
    /// </remarks>
    public static async ValueTask Acquire(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long lockKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var command = new NpgsqlCommand(AcquireSql, connection, transaction);
        command.Parameters.AddWithValue("lockKey", lockKey);
        _ = await command.ExecuteNonQueryAsync(ct);
    }
}
