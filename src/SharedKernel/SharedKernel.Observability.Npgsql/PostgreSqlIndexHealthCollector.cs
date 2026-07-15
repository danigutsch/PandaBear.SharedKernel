using Npgsql;

namespace SharedKernel.Observability.Npgsql;

/// <summary>Collects read-only PostgreSQL index-health evidence without applying database changes.</summary>
public sealed class PostgreSqlIndexHealthCollector
{
    private const string InsufficientPrivilegeSqlState = "42501";
    private const string FeatureNotSupportedSqlState = "0A000";
    private const int DefaultCommandTimeoutSeconds = 30;
    private const int MaximumCommandTimeoutSeconds = 300;
    private const string StatisticsWindowCommandText = """
        SELECT EXTRACT(EPOCH FROM clock_timestamp() - stats_reset)::bigint
        FROM pg_catalog.pg_stat_database
        WHERE datname = current_database();
        """;
    private const string IndexEvidenceCommandText = """
        WITH database_statistics AS (
            SELECT stats_reset
            FROM pg_catalog.pg_stat_database
            WHERE datname = current_database()
        )
        SELECT
            si.schemaname,
            si.relname,
            si.indexrelname,
            si.idx_scan,
            si.idx_tup_read,
            si.idx_tup_fetch,
            st.n_live_tup,
            i.indisprimary
                OR i.indisunique
                OR EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_constraint AS c
                    WHERE c.conindid = i.indexrelid) AS is_protected,
            i.indisvalid AND i.indisready AND i.indislive AS is_usable,
            i.indpred IS NULL AND i.indexprs IS NULL AS is_simple,
            (st.last_analyze IS NOT NULL OR st.last_autoanalyze IS NOT NULL)
                AND (
                    database_statistics.stats_reset IS NULL
                    OR GREATEST(
                        COALESCE(st.last_analyze, '-infinity'::timestamp with time zone),
                        COALESCE(st.last_autoanalyze, '-infinity'::timestamp with time zone))
                        >= database_statistics.stats_reset) AS statistics_are_reliable
        FROM pg_catalog.pg_stat_user_indexes AS si
        INNER JOIN pg_catalog.pg_stat_user_tables AS st ON st.relid = si.relid
        INNER JOIN pg_catalog.pg_index AS i ON i.indexrelid = si.indexrelid
        LEFT JOIN database_statistics ON TRUE;
        """;
    private const string TableEvidenceCommandText = """
        WITH database_statistics AS (
            SELECT stats_reset
            FROM pg_catalog.pg_stat_database
            WHERE datname = current_database()
        )
        SELECT
            st.schemaname,
            st.relname,
            st.seq_scan,
            st.seq_tup_read,
            st.n_live_tup,
            (st.last_analyze IS NOT NULL OR st.last_autoanalyze IS NOT NULL)
                AND (
                    database_statistics.stats_reset IS NULL
                    OR GREATEST(
                        COALESCE(st.last_analyze, '-infinity'::timestamp with time zone),
                        COALESCE(st.last_autoanalyze, '-infinity'::timestamp with time zone))
                        >= database_statistics.stats_reset) AS statistics_are_reliable
        FROM pg_catalog.pg_stat_user_tables AS st
        LEFT JOIN database_statistics ON TRUE;
        """;

    private readonly NpgsqlDataSource _dataSource;
    private readonly int _commandTimeoutSeconds;

    /// <summary>Initializes a collector with a dedicated read-only Npgsql data source.</summary>
    /// <param name="dataSource">The data source created for a least-privilege monitoring role.</param>
    /// <param name="commandTimeout">The optional maximum duration for each read-only command.</param>
    public PostgreSqlIndexHealthCollector(NpgsqlDataSource dataSource, TimeSpan? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var effectiveCommandTimeout = commandTimeout ?? TimeSpan.FromSeconds(DefaultCommandTimeoutSeconds);
        if (effectiveCommandTimeout <= TimeSpan.Zero || effectiveCommandTimeout > TimeSpan.FromSeconds(MaximumCommandTimeoutSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeout));
        }

        _dataSource = dataSource;
        _commandTimeoutSeconds = (int)Math.Ceiling(effectiveCommandTimeout.TotalSeconds);
    }

    /// <summary>Collects PostgreSQL catalog evidence and emits only bounded aggregate telemetry.</summary>
    /// <param name="ct">The token that stops the collection cooperatively.</param>
    /// <returns>The bounded collection outcome and in-memory advisory evidence.</returns>
    public async ValueTask<PostgreSqlIndexHealthCollectionResult> Collect(CancellationToken ct)
    {
        try
        {
            var connection = await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using (connection)
            {
                var statisticsWindow = await GetStatisticsWindow(connection, ct).ConfigureAwait(false);
                var assessments = new List<PostgreSqlIndexHealthAssessment>();

                await CollectIndexEvidence(connection, statisticsWindow, assessments, ct).ConfigureAwait(false);
                await CollectTableEvidence(connection, statisticsWindow, assessments, ct).ConfigureAwait(false);

                var result = new PostgreSqlIndexHealthCollectionResult(
                    PostgreSqlIndexHealthCollectionOutcome.Collected,
                    assessments);
                PostgreSqlIndexHealthTelemetry.Record(result);
                return result;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (NpgsqlException exception)
        {
            return CreateUnavailableResult(GetCollectionOutcome(exception));
        }
        catch (TimeoutException)
        {
            return CreateUnavailableResult(PostgreSqlIndexHealthCollectionOutcome.Unavailable);
        }
    }

    private static PostgreSqlIndexHealthCollectionResult CreateUnavailableResult(PostgreSqlIndexHealthCollectionOutcome outcome)
    {
        var result = new PostgreSqlIndexHealthCollectionResult(outcome, []);
        PostgreSqlIndexHealthTelemetry.Record(result);
        return result;
    }

    private static PostgreSqlIndexHealthCollectionOutcome GetCollectionOutcome(NpgsqlException exception)
    {
        return exception switch
        {
            PostgresException { SqlState: InsufficientPrivilegeSqlState } => PostgreSqlIndexHealthCollectionOutcome.PermissionDenied,
            PostgresException { SqlState: FeatureNotSupportedSqlState } => PostgreSqlIndexHealthCollectionOutcome.Unsupported,
            _ => PostgreSqlIndexHealthCollectionOutcome.Unavailable,
        };
    }

    private async ValueTask<TimeSpan?> GetStatisticsWindow(NpgsqlConnection connection, CancellationToken ct)
    {
        var command = CreateCommand(connection);
        await using (command)
        {
            command.CommandText = StatisticsWindowCommandText;
            var seconds = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (seconds is null or DBNull)
            {
                return null;
            }

            return TimeSpan.FromSeconds(Convert.ToInt64(seconds, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private async ValueTask CollectIndexEvidence(
        NpgsqlConnection connection,
        TimeSpan? statisticsWindow,
        List<PostgreSqlIndexHealthAssessment> assessments,
        CancellationToken ct)
    {
        var command = CreateCommand(connection);
        await using (command)
        {
            command.CommandText = IndexEvidenceCommandText;
            var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader)
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var evidence = new PostgreSqlIndexEvidence
                    {
                        Kind = PostgreSqlIndexEvidenceKind.Index,
                        SchemaName = reader.GetString(0),
                        TableName = reader.GetString(1),
                        IndexName = reader.GetString(2),
                        ScanCount = reader.GetInt64(3),
                        TuplesRead = reader.GetInt64(4),
                        TuplesFetched = reader.GetInt64(5),
                        EstimatedRows = reader.GetInt64(6),
                        IsProtected = reader.GetBoolean(7),
                        IsUsable = reader.GetBoolean(8),
                        IsSimple = reader.GetBoolean(9),
                        StatisticsAreReliable = reader.GetBoolean(10),
                        StatisticsWindow = statisticsWindow,
                    };

                    assessments.Add(PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence));
                }
            }
        }
    }

    private async ValueTask CollectTableEvidence(
        NpgsqlConnection connection,
        TimeSpan? statisticsWindow,
        List<PostgreSqlIndexHealthAssessment> assessments,
        CancellationToken ct)
    {
        var command = CreateCommand(connection);
        await using (command)
        {
            command.CommandText = TableEvidenceCommandText;
            var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader)
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    var evidence = new PostgreSqlIndexEvidence
                    {
                        Kind = PostgreSqlIndexEvidenceKind.Table,
                        SchemaName = reader.GetString(0),
                        TableName = reader.GetString(1),
                        IndexName = null,
                        ScanCount = reader.GetInt64(2),
                        TuplesRead = reader.GetInt64(3),
                        TuplesFetched = 0,
                        EstimatedRows = reader.GetInt64(4),
                        IsProtected = false,
                        IsUsable = true,
                        IsSimple = true,
                        StatisticsAreReliable = reader.GetBoolean(5),
                        StatisticsWindow = statisticsWindow,
                    };

                    assessments.Add(PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence));
                }
            }
        }
    }

    private NpgsqlCommand CreateCommand(NpgsqlConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandTimeout = _commandTimeoutSeconds;
        return command;
    }
}
