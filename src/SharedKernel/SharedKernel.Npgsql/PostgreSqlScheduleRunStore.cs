using Npgsql;
using NpgsqlTypes;

namespace PandaBear.Npgsql;

/// <summary>Records and reads durable scheduling observations in an application-owned PostgreSQL relation.</summary>
/// <remarks>Hosts supply observed outcomes. Observation windows represent the freshness of a report.</remarks>
public sealed class PostgreSqlScheduleRunStore
{
    private const int MaximumPageSize = 100;
    private const int MaximumJobNameLength = 128;
    private const int CommandTimeoutSeconds = 30;
    private static readonly TimeSpan MinimumObservationWindow = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaximumObservationWindow = TimeSpan.FromDays(1);
    private readonly NpgsqlDataSource dataSource;
    private readonly string relation;

    /// <summary>Creates a store for a qualified relation implementing the scheduling observation schema.</summary>
    /// <param name="dataSource">The caller-owned PostgreSQL data source.</param>
    /// <param name="schema">The schema of the observations.</param>
    /// <param name="relationName">The observation table, or a metadata view for reads.</param>
    /// <exception cref="ArgumentNullException">The data source or an identifier is null.</exception>
    /// <exception cref="ArgumentException">An identifier is empty or contains NUL.</exception>
    public PostgreSqlScheduleRunStore(NpgsqlDataSource dataSource, string schema, string relationName)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        using var identifiers = new NpgsqlCommandBuilder();
        relation = $"{QuoteIdentifier(identifiers, schema)}.{QuoteIdentifier(identifiers, relationName)}";
    }

    /// <summary>Records a new run using the caller's stable run identifier.</summary>
    /// <param name="jobName">The exact configured job name.</param>
    /// <param name="runId">The identifier to reuse when retrying the same observation write.</param>
    /// <param name="scheduledFor">The provider's intended fire time, when known.</param>
    /// <param name="observationWindow">How long the start supports an active observation, from 1 ms to 1 day.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the run was inserted. An existing identifier keeps its original observation.</returns>
    /// <exception cref="ArgumentException">The job name is blank or exceeds 128 characters, or the run identifier is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The observation window falls outside its supported range.</exception>
    public async Task<bool> Start(
        string jobName, Guid runId, DateTimeOffset? scheduledFor, TimeSpan observationWindow, CancellationToken ct)
    {
        ValidateRun(jobName, runId);
        ValidateObservationWindow(observationWindow);
        ct.ThrowIfCancellationRequested();

        await using var command = CreateCommand(
            $"""
            INSERT INTO {relation} (job_name, run_id, started_at, scheduled_for, observed_until)
            VALUES (@job_name, @run_id, pg_catalog.statement_timestamp(), @scheduled_for,
                pg_catalog.statement_timestamp() + @observation_window)
            ON CONFLICT (run_id) DO NOTHING
            """);
        AddRunParameters(command, jobName, runId);
        command.Parameters.AddWithValue("scheduled_for", NpgsqlDbType.TimestampTz, DatabaseTimestamp(scheduledFor));
        command.Parameters.AddWithValue("observation_window", NpgsqlDbType.Interval, observationWindow);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    /// <summary>Renews an unfinished run's active observation after the observer has confirmed activity.</summary>
    /// <param name="jobName">The exact configured job name.</param>
    /// <param name="runId">The active run's identifier.</param>
    /// <param name="observationWindow">How long the renewed report supports an active observation, from 1 ms to 1 day.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether the matching unfinished observation was found.</returns>
    /// <exception cref="ArgumentException">The job name is blank or exceeds 128 characters, or the run identifier is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The observation window falls outside its supported range.</exception>
    public async Task<bool> Refresh(string jobName, Guid runId, TimeSpan observationWindow, CancellationToken ct)
    {
        ValidateRun(jobName, runId);
        ValidateObservationWindow(observationWindow);
        ct.ThrowIfCancellationRequested();

        await using var command = CreateCommand(
            $"""
            UPDATE {relation}
            SET observed_until = GREATEST(observed_until, pg_catalog.statement_timestamp() + @observation_window)
            WHERE job_name COLLATE pg_catalog."C" = @job_name AND run_id = @run_id AND outcome IS NULL
            """);
        AddRunParameters(command, jobName, runId);
        command.Parameters.AddWithValue("observation_window", NpgsqlDbType.Interval, observationWindow);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    /// <summary>Records the first observed completion for a run.</summary>
    /// <param name="jobName">The exact configured job name.</param>
    /// <param name="runId">The run's identifier.</param>
    /// <param name="outcome">The observed success, cancellation, failure, retry, or terminal failure.</param>
    /// <param name="retryAt">The confirmed retry time, required only for a retry-scheduled result.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>Whether a matching unfinished run was completed.</returns>
    /// <exception cref="ArgumentException">The job, run identifier, or retry time is invalid for the requested observation.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The status does not describe a completion.</exception>
    public async Task<bool> Complete(
        string jobName, Guid runId, PostgreSqlScheduleRunStatus outcome, DateTimeOffset? retryAt, CancellationToken ct)
    {
        ValidateRun(jobName, runId);
        if (outcome is < PostgreSqlScheduleRunStatus.Succeeded or > PostgreSqlScheduleRunStatus.TerminalFailure)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The status must describe a run completion.");
        }

        if ((outcome == PostgreSqlScheduleRunStatus.RetryScheduled) != retryAt.HasValue)
        {
            throw new ArgumentException("A retry time is required only for the retry-scheduled status.", nameof(retryAt));
        }

        ct.ThrowIfCancellationRequested();
        await using var command = CreateCommand(
            $"""
            UPDATE {relation}
            SET finished_at = GREATEST(started_at, pg_catalog.statement_timestamp()), outcome = @outcome, retry_at = @retry_at
            WHERE job_name COLLATE pg_catalog."C" = @job_name AND run_id = @run_id AND outcome IS NULL
            """);
        AddRunParameters(command, jobName, runId);
        command.Parameters.AddWithValue("outcome", NpgsqlDbType.Integer, (int)outcome);
        command.Parameters.AddWithValue("retry_at", NpgsqlDbType.TimestampTz, DatabaseTimestamp(retryAt));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    /// <summary>Reads a stable page of one job's run history with a single metadata projection.</summary>
    /// <param name="jobName">The exact configured job name.</param>
    /// <param name="after">The exclusive cursor from a previous page of this job.</param>
    /// <param name="pageSize">The maximum number of observations to return, from one to 100.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page in descending order by start timestamp and PostgreSQL UUID order.</returns>
    /// <exception cref="ArgumentException">The job name is blank or exceeds 128 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page size falls outside its supported range.</exception>
    public Task<PostgreSqlScheduleRunPage> ReadPage(
        string jobName, PostgreSqlScheduleRunCursor? after, int pageSize, CancellationToken ct) =>
        ReadPageCore(jobName, after, pageSize, activeOnly: false, ct);

    /// <summary>Reads a bounded page of unfinished runs with a fresh active observation for one job.</summary>
    /// <remarks>The database evaluates freshness separately for each page.</remarks>
    /// <param name="jobName">The exact configured job name.</param>
    /// <param name="after">The exclusive cursor from a previous active page of this job.</param>
    /// <param name="pageSize">The maximum number of observations to return, from one to 100.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The active observations in descending order by start timestamp and PostgreSQL UUID order.</returns>
    /// <exception cref="ArgumentException">The job name is blank or exceeds 128 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page size falls outside its supported range.</exception>
    public Task<PostgreSqlScheduleRunPage> ReadActivePage(
        string jobName, PostgreSqlScheduleRunCursor? after, int pageSize, CancellationToken ct) =>
        ReadPageCore(jobName, after, pageSize, activeOnly: true, ct);

    private async Task<PostgreSqlScheduleRunPage> ReadPageCore(
        string jobName, PostgreSqlScheduleRunCursor? after, int pageSize, bool activeOnly, CancellationToken ct)
    {
        ValidateJobName(jobName);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaximumPageSize);
        ct.ThrowIfCancellationRequested();

        var activeFilter = activeOnly
            ? "AND outcome IS NULL AND observed_until > pg_catalog.statement_timestamp()"
            : string.Empty;
        await using var command = CreateCommand(
            $"""
            SELECT run_id, started_at, scheduled_for, finished_at, retry_at,
                CASE
                    WHEN outcome IS NOT NULL THEN outcome
                    WHEN observed_until > pg_catalog.statement_timestamp() THEN {(int)PostgreSqlScheduleRunStatus.Running}
                    ELSE {(int)PostgreSqlScheduleRunStatus.Unknown}
                END
            FROM {relation}
            WHERE job_name COLLATE pg_catalog."C" = @job_name
                {activeFilter}
                AND (@after_started_at IS NULL OR (started_at, run_id) < (@after_started_at, @after_run_id))
            ORDER BY started_at DESC, run_id DESC
            LIMIT @row_limit
            """);
        command.Parameters.AddWithValue("job_name", NpgsqlDbType.Text, jobName);
        command.Parameters.AddWithValue("after_started_at", NpgsqlDbType.TimestampTz, DatabaseTimestamp(after?.StartedAt));
        command.Parameters.AddWithValue("after_run_id", NpgsqlDbType.Uuid, after is { } cursor ? cursor.RunId : DBNull.Value);
        command.Parameters.AddWithValue("row_limit", NpgsqlDbType.Integer, pageSize + 1);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var results = new List<PostgreSqlScheduleRun>(pageSize);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            if (results.Count == pageSize)
            {
                var last = results[^1];
                return new PostgreSqlScheduleRunPage(results.AsReadOnly(), new PostgreSqlScheduleRunCursor(last.StartedAt, last.RunId));
            }

            var startedAt = await reader.GetFieldValueAsync<DateTimeOffset>(1, ct).ConfigureAwait(false);
            results.Add(new PostgreSqlScheduleRun(
                reader.GetGuid(0), startedAt, ReadOptionalTimestamp(reader, 2),
                ReadOptionalTimestamp(reader, 3), ReadOptionalTimestamp(reader, 4), (PostgreSqlScheduleRunStatus)reader.GetInt32(5)));
        }

        return new PostgreSqlScheduleRunPage(results.AsReadOnly(), null);
    }

    private NpgsqlCommand CreateCommand(string sql)
    {
        var command = dataSource.CreateCommand(sql);
        command.CommandTimeout = CommandTimeoutSeconds;
        return command;
    }

    private static string QuoteIdentifier(NpgsqlCommandBuilder builder, string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (identifier.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A PostgreSQL identifier cannot contain a NUL character.", nameof(identifier));
        }

        return builder.QuoteIdentifier(identifier);
    }

    private static void AddRunParameters(NpgsqlCommand command, string jobName, Guid runId)
    {
        command.Parameters.AddWithValue("job_name", NpgsqlDbType.Text, jobName);
        command.Parameters.AddWithValue("run_id", NpgsqlDbType.Uuid, runId);
    }

    private static void ValidateJobName(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        if (jobName.Length > MaximumJobNameLength || jobName.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A configured job name has at most 128 characters and contains no NUL.", nameof(jobName));
        }
    }

    private static void ValidateRun(string jobName, Guid runId)
    {
        ValidateJobName(jobName);
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("The run identifier must not be empty.", nameof(runId));
        }
    }

    private static void ValidateObservationWindow(TimeSpan observationWindow)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(observationWindow, MinimumObservationWindow);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(observationWindow, MaximumObservationWindow);
    }

    private static object DatabaseTimestamp(DateTimeOffset? timestamp) =>
        timestamp is DateTimeOffset value ? value.ToUniversalTime() : DBNull.Value;

    private static DateTimeOffset? ReadOptionalTimestamp(NpgsqlDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetFieldValue<DateTimeOffset>(index);
}
