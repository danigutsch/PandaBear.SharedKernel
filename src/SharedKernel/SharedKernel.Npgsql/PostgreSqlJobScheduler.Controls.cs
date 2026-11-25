using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace SharedKernel.Npgsql;

public sealed partial class PostgreSqlJobScheduler
{
    /// <summary>Pauses or resumes acquisition for a configured job.</summary>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="paused">Whether to pause acquisitions. Current live owners may renew or complete.</param>
    /// <param name="ct">The token that cancels the update.</param>
    /// <returns>Whether the configured job was found.</returns>
    /// <exception cref="ArgumentException">The job name is invalid.</exception>
    public Task<bool> SetPaused(string jobName, bool paused, CancellationToken ct) =>
        SetPausedCore(jobName, paused, transaction: null, ct);

    /// <inheritdoc cref="SetPaused(string, bool, CancellationToken)"/>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="paused">Whether to pause acquisitions.</param>
    /// <param name="transaction">The caller-owned open transaction on the scheduler database.</param>
    /// <param name="ct">The token that cancels the update.</param>
    /// <exception cref="ArgumentNullException">The transaction is null.</exception>
    /// <exception cref="ArgumentException">The job name is invalid or the transaction has ended.</exception>
    public Task<bool> SetPaused(string jobName, bool paused, NpgsqlTransaction transaction, CancellationToken ct) =>
        SetPausedCore(jobName, paused, RequireTransaction(transaction), ct);

    /// <summary>Queues an immediate execution for a configured job.</summary>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="requestId">The stable identifier of this request and its resulting execution.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>Whether the request was accepted now or on an earlier equivalent call.</returns>
    /// <exception cref="ArgumentException">The job name or request identifier is invalid.</exception>
    public Task<bool> Trigger(string jobName, Guid requestId, CancellationToken ct) =>
        Enqueue(jobName, requestId, failedRunId: null, transaction: null, ct);

    /// <inheritdoc cref="Trigger(string, Guid, CancellationToken)"/>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="requestId">The stable identifier of this request and its resulting execution.</param>
    /// <param name="transaction">The caller-owned open transaction on the scheduler database.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <exception cref="ArgumentNullException">The transaction is null.</exception>
    /// <exception cref="ArgumentException">An identifier is invalid or the transaction has ended.</exception>
    public Task<bool> Trigger(string jobName, Guid requestId, NpgsqlTransaction transaction, CancellationToken ct) =>
        Enqueue(jobName, requestId, failedRunId: null, RequireTransaction(transaction), ct);

    /// <summary>Queues one new attempt for a failed execution of the configured job.</summary>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="failedRunId">The failed execution to retry.</param>
    /// <param name="requestId">The stable identifier of this request and the new execution.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <returns>Whether the request was accepted now or on an earlier equivalent call.</returns>
    /// <exception cref="ArgumentException">The job name or an execution identifier is invalid.</exception>
    public Task<bool> Retry(string jobName, Guid failedRunId, Guid requestId, CancellationToken ct) =>
        Enqueue(jobName, requestId, failedRunId, transaction: null, ct);

    /// <inheritdoc cref="Retry(string, Guid, Guid, CancellationToken)"/>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="failedRunId">The failed execution to retry.</param>
    /// <param name="requestId">The stable identifier of this request and the new execution.</param>
    /// <param name="transaction">The caller-owned open transaction on the scheduler database.</param>
    /// <param name="ct">The token that cancels the request.</param>
    /// <exception cref="ArgumentNullException">The transaction is null.</exception>
    /// <exception cref="ArgumentException">An identifier is invalid or the transaction has ended.</exception>
    public Task<bool> Retry(string jobName, Guid failedRunId, Guid requestId, NpgsqlTransaction transaction, CancellationToken ct) =>
        Enqueue(jobName, requestId, failedRunId, RequireTransaction(transaction), ct);

    /// <summary>Reads one configured job's persisted scheduling metadata.</summary>
    /// <exception cref="ArgumentException">The job name is invalid.</exception>
    public async Task<PostgreSqlJobSchedule?> ReadJob(string jobName, CancellationToken ct)
    {
        ValidateJobName(jobName);
        await using var command = CreateCommand($"SELECT next_due_at, fixed_delay_microseconds, paused FROM {jobs} WHERE job_name = @job_name");
        AddJobName(command, jobName);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        var due = await ReadTimestamp(reader, 0, ct).ConfigureAwait(false);
        var delay = await reader.IsDBNullAsync(1, ct).ConfigureAwait(false)
            ? (TimeSpan?)null : TimeSpan.FromTicks(reader.GetInt64(1) * TimeSpan.TicksPerMicrosecond);
        return new PostgreSqlJobSchedule(jobName, due, delay, reader.GetBoolean(2));
    }

    /// <summary>Reads a bounded page of started executions for the exact job.</summary>
    /// <exception cref="ArgumentException">The job name is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page size is outside 1 to 100.</exception>
    public Task<PostgreSqlScheduleRunPage> ReadHistory(
        string jobName, PostgreSqlScheduleRunCursor? after, int pageSize, CancellationToken ct) =>
        history.ReadPage(jobName, after, pageSize, ct);

    /// <summary>Reads a bounded page of currently leased executions for the exact job.</summary>
    /// <exception cref="ArgumentException">The job name is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page size is outside 1 to 100.</exception>
    public Task<PostgreSqlScheduleRunPage> ReadActive(
        string jobName, PostgreSqlScheduleRunCursor? after, int pageSize, CancellationToken ct) =>
        history.ReadActivePage(jobName, after, pageSize, ct);

    private async Task<bool> SetPausedCore(string jobName, bool paused, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        ValidateJobName(jobName);
        await using var command = CreateCommand($"UPDATE {jobs} SET paused = @paused WHERE job_name = @job_name", transaction);
        AddJobName(command, jobName);
        command.Parameters.AddWithValue("paused", NpgsqlDbType.Boolean, paused);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
    }

    private async Task<bool> Enqueue(
        string jobName, Guid requestId, Guid? failedRunId, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        ValidateJobName(jobName);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request identifier cannot be empty.", nameof(requestId));
        }

        if (failedRunId == Guid.Empty)
        {
            throw new ArgumentException("A failed execution identifier cannot be empty.", nameof(failedRunId));
        }

        if (transaction is not null)
        {
            return await EnqueueLocked(jobName, requestId, failedRunId, transaction, ct).ConfigureAwait(false);
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var owned = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        var accepted = await EnqueueLocked(jobName, requestId, failedRunId, owned, ct).ConfigureAwait(false);
        await owned.CommitAsync(ct).ConfigureAwait(false);
        return accepted;
    }

    private async Task<bool> EnqueueLocked(
        string jobName, Guid requestId, Guid? failedRunId, NpgsqlTransaction transaction, CancellationToken ct)
    {
        if (!await LockJob(jobName, transaction, ct).ConfigureAwait(false))
        {
            return false;
        }

        var kind = failedRunId is null ? PostgreSqlJobRunKind.Manual : PostgreSqlJobRunKind.Retry;
        await using var command = CreateCommand(
            $"""
            WITH enqueued AS (
                INSERT INTO {executions} (run_id, job_name, kind, retry_of, available_at)
                SELECT @request_id, @job_name, @kind, @failed_run_id, pg_catalog.statement_timestamp()
                WHERE @failed_run_id IS NULL OR EXISTS (
                    SELECT FROM {executions} AS previous
                    WHERE previous.job_name = @job_name AND previous.run_id = @failed_run_id
                        AND previous.outcome = {(int)PostgreSqlScheduleRunStatus.Failed}
                )
                ON CONFLICT (run_id) DO NOTHING
                RETURNING available_at
            ), record_retry AS (
                UPDATE {executions} AS previous
                SET outcome = {(int)PostgreSqlScheduleRunStatus.RetryScheduled}, retry_at = enqueued.available_at
                FROM enqueued
                WHERE previous.job_name = @job_name AND previous.run_id = @failed_run_id
            )
            SELECT EXISTS (SELECT FROM enqueued)
            """, transaction);
        AddRequest(command, jobName, requestId, failedRunId, kind);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is true)
        {
            return true;
        }

        // A conflict on the global request ID can be decided by a concurrent job after the first statement's snapshot.
        await using var verify = CreateCommand(
            $"""
            SELECT EXISTS (
                SELECT FROM {executions}
                WHERE run_id = @request_id AND job_name = @job_name AND kind = @kind
                    AND retry_of IS NOT DISTINCT FROM @failed_run_id
            )
            """, transaction);
        AddRequest(verify, jobName, requestId, failedRunId, kind);
        return await verify.ExecuteScalarAsync(ct).ConfigureAwait(false) is true;
    }

    private static void AddRequest(NpgsqlCommand command, string jobName, Guid requestId, Guid? failedRunId, PostgreSqlJobRunKind kind)
    {
        AddJobName(command, jobName);
        command.Parameters.AddWithValue("request_id", NpgsqlDbType.Uuid, requestId);
        command.Parameters.AddWithValue("kind", NpgsqlDbType.Integer, (int)kind);
        command.Parameters.AddWithValue("failed_run_id", NpgsqlDbType.Uuid, failedRunId is { } prior ? prior : DBNull.Value);
    }

    private static NpgsqlTransaction RequireTransaction(NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.Connection is null
            ? throw new ArgumentException("An open transaction on the scheduler database is required.", nameof(transaction))
            : transaction;
    }
}
