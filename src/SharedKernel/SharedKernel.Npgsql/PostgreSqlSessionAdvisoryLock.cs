using Npgsql;

namespace SharedKernel.Npgsql;

/// <summary>
/// Acquires PostgreSQL advisory locks held by dedicated database sessions.
/// </summary>
public static class PostgreSqlSessionAdvisoryLock
{
    private const string AcquireSql = "SELECT pg_advisory_lock(@lockKey);";
    private const string ReleaseSql = "SELECT pg_advisory_unlock(@lockKey);";

    /// <summary>
    /// Acquires an exclusive session-scoped advisory lock for <paramref name="lockKey" />.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source used to create the lock-owning session.</param>
    /// <param name="lockKey">The caller-defined 64-bit advisory lock key.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A lease whose disposal closes the session and releases the lock.</returns>
    /// <remarks>Callers own lock-key derivation and must retain the lease through their critical section.</remarks>
    public static async ValueTask<IAsyncDisposable> Acquire(
        NpgsqlDataSource dataSource,
        long lockKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            using var command = new NpgsqlCommand(AcquireSql, connection);
            command.Parameters.AddWithValue("lockKey", lockKey);
            _ = await command.ExecuteNonQueryAsync(ct);
            return new Lease(connection, lockKey);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(NpgsqlConnection connection, long lockKey) : IAsyncDisposable, IDisposable
    {
        private readonly long lockKey = lockKey;
        private NpgsqlConnection? connection = connection;

        public void Dispose()
        {
            if (connection is null)
            {
                return;
            }

            try
            {
                using var command = new NpgsqlCommand(ReleaseSql, connection);
                command.Parameters.AddWithValue("lockKey", lockKey);
                _ = command.ExecuteScalar();
            }
            finally
            {
                connection.Dispose();
                connection = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (connection is null)
            {
                return;
            }

            try
            {
                using var command = new NpgsqlCommand(ReleaseSql, connection);
                command.Parameters.AddWithValue("lockKey", lockKey);
                _ = await command.ExecuteScalarAsync(CancellationToken.None);
            }
            finally
            {
                await connection.DisposeAsync();
                connection = null;
            }
        }
    }
}
