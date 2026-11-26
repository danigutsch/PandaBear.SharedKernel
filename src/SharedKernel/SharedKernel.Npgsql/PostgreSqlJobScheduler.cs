using System.Data;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace SharedKernel.Npgsql;

/// <summary>Coordinates durable job executions across hosts sharing an application-owned PostgreSQL schema.</summary>
/// <remarks>Unfinished execution can be delivered again after lease expiry. Use a repeatable handler keyed by the stable run ID.</remarks>
public sealed partial class PostgreSqlJobScheduler
{
    private const string JobTable = "scheduler_jobs";
    private const string ExecutionTable = "scheduler_executions";
    private const string RunningJobIndex = "scheduler_executions_running_job";
    private const int CommandTimeoutSeconds = 30;
    private const int MaximumJobNameLength = 128;
    private readonly NpgsqlDataSource dataSource;
    private readonly string jobs;
    private readonly string executions;
    private readonly string readyFrom;
    private readonly PostgreSqlScheduleRunStore history;

    /// <summary>Creates a scheduler using the two scheduler tables in an existing application-owned schema.</summary>
    /// <param name="dataSource">The caller-owned PostgreSQL data source.</param>
    /// <param name="schema">The PostgreSQL schema identifier.</param>
    /// <exception cref="ArgumentNullException">The data source or schema is null.</exception>
    /// <exception cref="ArgumentException">The schema is empty, contains NUL, or exceeds 63 UTF-8 bytes.</exception>
    public PostgreSqlJobScheduler(NpgsqlDataSource dataSource, string schema)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        (jobs, executions) = QualifyTables(schema);
        history = new PostgreSqlScheduleRunStore(dataSource, schema, ExecutionTable);
        readyFrom =
            $"""
            FROM {jobs} AS job
            LEFT JOIN LATERAL (
                SELECT run_id, started_at, observed_until, available_at
                FROM {executions}
                WHERE job_name = job.job_name AND outcome IS NULL
                ORDER BY (started_at IS NOT NULL) DESC, available_at, run_id
                LIMIT 1
            ) AS waiting ON true
            """;
    }

    /// <summary>Creates the scheduler tables and indexes and verifies the running-job index.</summary>
    /// <param name="ct">The token that cancels schema creation.</param>
    /// <remarks>Run this explicitly through the schema-owner migration path before using the scheduler.</remarks>
    /// <exception cref="InvalidOperationException">The installed running-job index has an incompatible definition.</exception>
    public async Task InitializeSchema(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        await using var command = CreateCommand(CreateSchemaSql(jobs, executions), transaction);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await VerifyRunningJobIndex(transaction, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    private async Task VerifyRunningJobIndex(NpgsqlTransaction transaction, CancellationToken ct)
    {
        // Match PostgreSQL's non-pretty deparse of the partial predicate defined by CreateSchemaSql.
        await using var command = CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_catalog.pg_index AS definition
                JOIN pg_catalog.pg_class AS index_relation
                    ON index_relation.oid = definition.indexrelid
                JOIN pg_catalog.pg_attribute AS key_column
                    ON key_column.attrelid = definition.indrelid
                    AND key_column.attnum = definition.indkey[0]
                WHERE definition.indrelid = pg_catalog.to_regclass(@executions)
                    AND index_relation.relname = @index_name
                    AND definition.indnkeyatts = 1
                    AND definition.indisunique
                    AND definition.indimmediate
                    AND definition.indisvalid
                    AND definition.indisready
                    AND definition.indislive
                    AND key_column.attname = 'job_name'
                    AND pg_catalog.pg_get_expr(definition.indpred, definition.indrelid, false) =
                        '((started_at IS NOT NULL) AND (outcome IS NULL))'
            )
            """, transaction);
        command.Parameters.AddWithValue("executions", NpgsqlDbType.Text, executions);
        command.Parameters.AddWithValue("index_name", NpgsqlDbType.Text, RunningJobIndex);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
        {
            throw new InvalidOperationException($"The installed {RunningJobIndex} index has an incompatible definition.");
        }
    }

    /// <summary>Returns the scheduler DDL for a governed migration in an existing schema.</summary>
    /// <param name="schema">The target PostgreSQL schema identifier.</param>
    /// <returns>The DDL that <see cref="InitializeSchema"/> executes before verifying the running-job index.</returns>
    /// <exception cref="ArgumentNullException">The schema is null.</exception>
    /// <exception cref="ArgumentException">The schema is empty, contains NUL, or exceeds 63 UTF-8 bytes.</exception>
    public static string CreateSchemaSql(string schema)
    {
        var (jobs, executions) = QualifyTables(schema);
        return CreateSchemaSql(jobs, executions);
    }

    private static string CreateSchemaSql(string jobs, string executions) =>
            $"""
            CREATE TABLE IF NOT EXISTS {jobs} (
                job_name text COLLATE pg_catalog."C" PRIMARY KEY,
                next_due_at timestamptz NULL,
                fixed_delay_microseconds bigint NULL CHECK (fixed_delay_microseconds > 0),
                paused boolean NOT NULL DEFAULT false,
                CHECK (pg_catalog.length(job_name) BETWEEN 1 AND {MaximumJobNameLength})
            );
            CREATE TABLE IF NOT EXISTS {executions} (
                run_id uuid PRIMARY KEY,
                job_name text COLLATE pg_catalog."C" NOT NULL REFERENCES {jobs}(job_name),
                kind integer NOT NULL CHECK (kind IN (0, 1, 2)),
                available_at timestamptz NOT NULL,
                scheduled_for timestamptz NULL,
                retry_of uuid NULL,
                started_at timestamptz NULL,
                observed_until timestamptz NULL,
                lease_fence uuid NULL,
                finished_at timestamptz NULL,
                outcome integer NULL CHECK (outcome IN (2, 3, 4, 5, 6)),
                retry_at timestamptz NULL,
                CHECK ((kind = 0) = (scheduled_for IS NOT NULL)),
                CHECK ((kind = 2) = (retry_of IS NOT NULL)),
                CHECK ((started_at IS NULL) = (observed_until IS NULL)),
                CHECK ((started_at IS NOT NULL AND outcome IS NULL) = (lease_fence IS NOT NULL)),
                CHECK ((finished_at IS NULL) = (outcome IS NULL)),
                CHECK (finished_at IS NULL OR started_at IS NOT NULL),
                CHECK (COALESCE(outcome = 5, false) = (retry_at IS NOT NULL))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS {RunningJobIndex}
                ON {executions}(job_name) WHERE started_at IS NOT NULL AND outcome IS NULL;
            CREATE INDEX IF NOT EXISTS scheduler_executions_ready
                ON {executions}(job_name, (started_at IS NOT NULL) DESC, available_at, run_id) WHERE outcome IS NULL;
            CREATE INDEX IF NOT EXISTS scheduler_executions_history
                ON {executions}(job_name, started_at DESC, run_id DESC) WHERE started_at IS NOT NULL;
            CREATE INDEX IF NOT EXISTS scheduler_jobs_due
                ON {jobs}(next_due_at, job_name) WHERE NOT paused;
            """;

    /// <summary>Registers a configured job when its stable name is first declared.</summary>
    /// <param name="jobName">The exact configured name.</param>
    /// <param name="firstDueAt">The initial due time. Null permits explicit runs without an initial scheduled run.</param>
    /// <param name="fixedDelay">The elapsed delay after scheduled completion, or null for caller-defined recurrence.</param>
    /// <param name="ct">The token that cancels registration.</param>
    /// <returns>Whether a new schedule was inserted. Repeated registration preserves its persisted state.</returns>
    /// <exception cref="ArgumentException">The configured name is blank, too long, or contains NUL.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The delay is less than one microsecond.</exception>
    public async Task<bool> Register(string jobName, DateTimeOffset? firstDueAt, TimeSpan? fixedDelay, CancellationToken ct)
    {
        ValidateJobName(jobName);
        var delay = fixedDelay is { } value ? ToMicroseconds(value, nameof(fixedDelay)) : (long?)null;
        await using var command = CreateCommand(
            $"""
            INSERT INTO {jobs} (job_name, next_due_at, fixed_delay_microseconds)
            VALUES (@job_name, @first_due_at, @fixed_delay_microseconds)
            ON CONFLICT (job_name) DO NOTHING
            """);
        AddJobName(command, jobName);
        command.Parameters.AddWithValue("first_due_at", NpgsqlDbType.TimestampTz, Timestamp(firstDueAt));
        command.Parameters.AddWithValue("fixed_delay_microseconds", NpgsqlDbType.Bigint, delay is { } microseconds ? microseconds : DBNull.Value);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
    }

    /// <summary>Acquires a due, unpaused job and commits its exclusive lease.</summary>
    /// <param name="leaseDuration">The duration of the lease, at least one microsecond.</param>
    /// <param name="ct">The token that cancels acquisition.</param>
    /// <returns>The lease, or null when no available job was found. Expired executions retain their run ID.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The lease duration is less than one microsecond.</exception>
    public async Task<PostgreSqlJobLease?> Acquire(TimeSpan leaseDuration, CancellationToken ct)
    {
        var duration = ToMicroseconds(leaseDuration, nameof(leaseDuration));
        ct.ThrowIfCancellationRequested();
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        await using var find = CreateCommand(
            $"""
            SELECT job.job_name
            {readyFrom}
            WHERE {ReadyPredicate}
            ORDER BY
                CASE WHEN waiting.started_at IS NOT NULL THEN waiting.observed_until
                    ELSE LEAST(job.next_due_at, waiting.available_at) END,
                job.job_name
            LIMIT 1
            FOR UPDATE OF job SKIP LOCKED
            """, transaction);
        if (await find.ExecuteScalarAsync(ct).ConfigureAwait(false) is not string jobName)
        {
            return null;
        }

        // Re-read after grabbing the row. A previous holder can change executions without changing the job row.
        var work = await ReadReadyWork(jobName, transaction, ct).ConfigureAwait(false);
        if (work is null)
        {
            return null;
        }

        var fence = Guid.NewGuid();
        var lease = work.Value.RunId is { } run
            ? await AcquireExisting(jobName, run, fence, duration, transaction, ct).ConfigureAwait(false)
            : await AcquireScheduled(jobName, work.Value.NextDueAt!.Value, fence, duration, transaction, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return lease;
    }

    /// <summary>Extends a live owner’s lease without shortening it.</summary>
    /// <param name="lease">The acquired lease identifying the current fence.</param>
    /// <param name="leaseDuration">The duration from database time, at least one microsecond.</param>
    /// <param name="ct">The token that cancels renewal.</param>
    /// <returns>Whether the run still belonged to this unexpired owner.</returns>
    /// <exception cref="ArgumentNullException">The lease is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The lease duration is less than one microsecond.</exception>
    public async Task<bool> Renew(PostgreSqlJobLease lease, TimeSpan leaseDuration, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var duration = ToMicroseconds(leaseDuration, nameof(leaseDuration));
        ct.ThrowIfCancellationRequested();
        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        if (!await LockJob(lease.JobName, transaction, ct).ConfigureAwait(false))
        {
            return false;
        }

        await using var command = CreateCommand(
            $"""
            UPDATE {executions}
            SET observed_until = GREATEST(observed_until, pg_catalog.statement_timestamp() + @duration * interval '1 microsecond')
            WHERE {LiveFencePredicate}
            """, transaction);
        AddLease(command, lease);
        command.Parameters.AddWithValue("duration", NpgsqlDbType.Bigint, duration);
        var renewed = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return renewed;
    }

    /// <summary>Completes a run only while the acquired fence remains current and unexpired.</summary>
    /// <param name="lease">The acquired lease identifying the current fence.</param>
    /// <param name="outcome">Success, cancellation, failure, or terminal failure.</param>
    /// <param name="nextDueAt">The next due time for a caller-scheduled recurrence. Fixed-delay jobs use database completion time.</param>
    /// <param name="ct">The token that cancels completion.</param>
    /// <returns>Whether the run was completed. Explicit runs do not advance the normal schedule.</returns>
    /// <exception cref="ArgumentNullException">The lease is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The outcome does not describe a completion.</exception>
    public async Task<bool> Complete(PostgreSqlJobLease lease, PostgreSqlScheduleRunStatus outcome, DateTimeOffset? nextDueAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (outcome is not (PostgreSqlScheduleRunStatus.Succeeded or PostgreSqlScheduleRunStatus.Cancelled
            or PostgreSqlScheduleRunStatus.Failed or PostgreSqlScheduleRunStatus.TerminalFailure))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The outcome must describe a completion.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        if (!await LockJob(lease.JobName, transaction, ct).ConfigureAwait(false))
        {
            return false;
        }

        await using var command = CreateCommand(
            $"""
            WITH completed AS (
                UPDATE {executions}
                SET outcome = @outcome,
                    finished_at = GREATEST(started_at, pg_catalog.statement_timestamp()),
                    lease_fence = NULL
                WHERE {LiveFencePredicate}
                RETURNING job_name, kind, finished_at
            ), advance_schedule AS (
                UPDATE {jobs} AS job
                SET next_due_at = CASE WHEN job.fixed_delay_microseconds IS NULL THEN @next_due_at
                    ELSE completed.finished_at + job.fixed_delay_microseconds * interval '1 microsecond' END
                FROM completed
                WHERE job.job_name = completed.job_name AND completed.kind = {(int)PostgreSqlJobRunKind.Scheduled}
            )
            SELECT EXISTS (SELECT FROM completed)
            """, transaction);
        AddLease(command, lease);
        command.Parameters.AddWithValue("outcome", NpgsqlDbType.Integer, (int)outcome);
        command.Parameters.AddWithValue("next_due_at", NpgsqlDbType.TimestampTz, Timestamp(nextDueAt));
        var completed = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is true;
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return completed;
    }

    private const string ReadyPredicate =
        """
        NOT job.paused AND (
            (waiting.started_at IS NOT NULL AND waiting.observed_until <= pg_catalog.statement_timestamp())
            OR (waiting.started_at IS NULL AND LEAST(job.next_due_at, waiting.available_at) <= pg_catalog.statement_timestamp())
        )
        """;

    private const string LiveFencePredicate =
        """
        job_name = @job_name AND run_id = @run_id AND lease_fence = @fence AND outcome IS NULL
            AND observed_until > pg_catalog.statement_timestamp()
        """;

    private async Task<(Guid? RunId, DateTimeOffset? NextDueAt)?> ReadReadyWork(
        string jobName, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = CreateCommand(
            $"""
            SELECT CASE WHEN waiting.started_at IS NOT NULL OR job.next_due_at IS NULL
                OR waiting.available_at <= job.next_due_at THEN waiting.run_id END, job.next_due_at
            {readyFrom}
            WHERE job.job_name = @job_name AND {ReadyPredicate}
            """, transaction);
        AddJobName(command, jobName);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        var runId = await reader.IsDBNullAsync(0, ct).ConfigureAwait(false) ? (Guid?)null : reader.GetGuid(0);
        return (runId, await ReadTimestamp(reader, 1, ct).ConfigureAwait(false));
    }

    private async Task<PostgreSqlJobLease> AcquireExisting(
        string jobName, Guid run, Guid fence, long duration, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = CreateCommand(
            $"""
            UPDATE {executions}
            SET started_at = COALESCE(started_at, pg_catalog.statement_timestamp()),
                observed_until = pg_catalog.statement_timestamp() + @duration * interval '1 microsecond',
                lease_fence = @fence
            WHERE job_name = @job_name AND run_id = @run_id
            RETURNING observed_until, scheduled_for, kind
            """, transaction);
        AddAcquisition(command, jobName, run, fence, duration);
        return await ReadAcquired(command, jobName, run, fence, ct).ConfigureAwait(false);
    }

    private async Task<PostgreSqlJobLease> AcquireScheduled(
        string jobName, DateTimeOffset due, Guid fence, long duration, NpgsqlTransaction transaction, CancellationToken ct)
    {
        var run = Guid.NewGuid();
        await using var command = CreateCommand(
            $"""
            WITH reserve AS (
                UPDATE {jobs} SET next_due_at = NULL WHERE job_name = @job_name
            )
            INSERT INTO {executions} (
                run_id, job_name, kind, available_at, scheduled_for, started_at, observed_until, lease_fence
            )
            VALUES (@run_id, @job_name, {(int)PostgreSqlJobRunKind.Scheduled}, @due, @due, pg_catalog.statement_timestamp(),
                pg_catalog.statement_timestamp() + @duration * interval '1 microsecond', @fence)
            RETURNING observed_until, scheduled_for, kind
            """, transaction);
        AddAcquisition(command, jobName, run, fence, duration);
        command.Parameters.AddWithValue("due", NpgsqlDbType.TimestampTz, due);
        return await ReadAcquired(command, jobName, run, fence, ct).ConfigureAwait(false);
    }

    private static async Task<PostgreSqlJobLease> ReadAcquired(
        NpgsqlCommand command, string jobName, Guid run, Guid fence, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The locked scheduler execution could not be acquired.");
        }

        var expiresAt = await reader.GetFieldValueAsync<DateTimeOffset>(0, ct).ConfigureAwait(false);
        var scheduledFor = await ReadTimestamp(reader, 1, ct).ConfigureAwait(false);
        return new PostgreSqlJobLease(jobName, run, fence, expiresAt, scheduledFor,
            (PostgreSqlJobRunKind)reader.GetInt32(2));
    }

    private async Task<bool> LockJob(string jobName, NpgsqlTransaction transaction, CancellationToken ct)
    {
        await using var command = CreateCommand($"SELECT 1 FROM {jobs} WHERE job_name = @job_name FOR UPDATE", transaction);
        AddJobName(command, jobName);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is 1;
    }

    private NpgsqlCommand CreateCommand(string sql, NpgsqlTransaction? transaction = null)
    {
        var command = transaction is null
            ? dataSource.CreateCommand(sql)
            : new NpgsqlCommand(sql, transaction.Connection, transaction);
        command.CommandTimeout = CommandTimeoutSeconds;
        return command;
    }

    private static void AddLease(NpgsqlCommand command, PostgreSqlJobLease lease)
    {
        AddJobName(command, lease.JobName);
        command.Parameters.AddWithValue("run_id", NpgsqlDbType.Uuid, lease.RunId);
        command.Parameters.AddWithValue("fence", NpgsqlDbType.Uuid, lease.Fence);
    }

    private static void AddAcquisition(NpgsqlCommand command, string jobName, Guid run, Guid fence, long duration)
    {
        AddJobName(command, jobName);
        command.Parameters.AddWithValue("run_id", NpgsqlDbType.Uuid, run);
        command.Parameters.AddWithValue("fence", NpgsqlDbType.Uuid, fence);
        command.Parameters.AddWithValue("duration", NpgsqlDbType.Bigint, duration);
    }

    private static void AddJobName(NpgsqlCommand command, string jobName) =>
        command.Parameters.AddWithValue("job_name", NpgsqlDbType.Text, jobName);

    private static (string Jobs, string Executions) QualifyTables(string schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        if (schema.Contains('\0', StringComparison.Ordinal) || Encoding.UTF8.GetByteCount(schema) > 63)
        {
            throw new ArgumentException("The schema must be a PostgreSQL identifier that fits in 63 UTF-8 bytes.", nameof(schema));
        }

        using var names = new NpgsqlCommandBuilder();
        var qualifiedSchema = names.QuoteIdentifier(schema);
        return ($"{qualifiedSchema}.{JobTable}", $"{qualifiedSchema}.{ExecutionTable}");
    }

    private static void ValidateJobName(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        if (jobName.Length > MaximumJobNameLength || jobName.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A configured job name has at most 128 characters and contains no NUL.", nameof(jobName));
        }
    }

    private static long ToMicroseconds(TimeSpan duration, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.FromMicroseconds(1), paramName);
        return duration.Ticks / TimeSpan.TicksPerMicrosecond;
    }

    private static object Timestamp(DateTimeOffset? value) =>
        value is { } timestamp ? timestamp.ToUniversalTime() : DBNull.Value;

    private static async Task<DateTimeOffset?> ReadTimestamp(NpgsqlDataReader reader, int index, CancellationToken ct) =>
        await reader.IsDBNullAsync(index, ct).ConfigureAwait(false)
            ? null : await reader.GetFieldValueAsync<DateTimeOffset>(index, ct).ConfigureAwait(false);
}
