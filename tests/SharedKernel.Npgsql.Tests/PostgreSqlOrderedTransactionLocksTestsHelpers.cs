namespace PandaBear.Npgsql.Tests;

/// <summary>Helpers for the ordered transaction lock tests.</summary>
internal static class PostgreSqlOrderedTransactionLocksTestsHelpers
{
    /// <summary>Returns the names sorted by their advisory lock key, so a test can rely on which one sorts first.</summary>
    public static string[] NamesByKey(params string[] names) =>
        [.. names.OrderBy(PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian)];

    /// <summary>Returns the names whose lock a fresh connection can take right now, in the order given.</summary>
    public static async Task<string[]> FreeNames(NpgsqlDataSource dataSource, IEnumerable<string> names, CancellationToken ct)
    {
        var free = new List<string>();
        foreach (var name in names)
        {
            if (await PostgreSqlTransactionAdvisoryLockTestsHelpers.TryAcquire(dataSource, PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(name), ct))
            {
                free.Add(name);
            }
        }

        return [.. free];
    }

    /// <summary>Holds the named lock on its own connection and transaction until released or disposed.</summary>
    public static async Task<HeldLock> Hold(NpgsqlDataSource dataSource, string name, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        var transaction = await connection.BeginTransactionAsync(ct);
        await PostgreSqlTransactionAdvisoryLock.Acquire(connection, transaction, PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(name), ct);
        return new HeldLock(connection, transaction);
    }

    /// <summary>A lock held by a separate transaction.</summary>
    internal sealed class HeldLock(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        /// <summary>Rolls the holding transaction back, which releases the lock.</summary>
        public async Task Release(CancellationToken ct) => await transaction.RollbackAsync(ct);

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
