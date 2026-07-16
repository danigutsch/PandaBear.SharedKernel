namespace SharedKernel.Npgsql.Tests;

internal static class PostgreSqlTransactionAdvisoryLockTestsHelpers
{
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(10);

    public static async Task<bool> TryAcquire(NpgsqlDataSource dataSource, long lockKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(@lockKey);", connection, transaction);
        command.Parameters.AddWithValue("lockKey", lockKey);
        var result = await command.ExecuteScalarAsync(ct);
        await transaction.RollbackAsync(ct);

        return result is true;
    }

    public static async Task WaitForWaitingLock(NpgsqlDataSource dataSource, int processId, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(LockWaitTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            while (!linkedCts.IsCancellationRequested)
            {
                await using var connection = await dataSource.OpenConnectionAsync(linkedCts.Token);
                await using var command = new NpgsqlCommand(
                    "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND pid = @processId);",
                    connection);
                command.Parameters.AddWithValue("processId", processId);
                var isWaiting = await command.ExecuteScalarAsync(linkedCts.Token);
                if (isWaiting is true)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25), linkedCts.Token);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("The PostgreSQL advisory lock acquisition did not begin waiting.");
        }

        throw new TimeoutException("The PostgreSQL advisory lock acquisition did not begin waiting.");
    }
}
