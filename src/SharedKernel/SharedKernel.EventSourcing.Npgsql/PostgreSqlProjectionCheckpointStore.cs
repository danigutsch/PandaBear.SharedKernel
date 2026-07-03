using System.Diagnostics;
using Npgsql;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.EventSourcing.Npgsql;

/// <summary>
/// Persists projection checkpoints in PostgreSQL.
/// </summary>
public sealed class PostgreSqlProjectionCheckpointStore : IProjectionCheckpointStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly PostgreSqlEventSourcingOptions options;
    private readonly bool ownsDataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlProjectionCheckpointStore" /> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public PostgreSqlProjectionCheckpointStore(
        string connectionString)
        : this(connectionString, options: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlProjectionCheckpointStore" /> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="options">The provider options.</param>
    public PostgreSqlProjectionCheckpointStore(
        string connectionString,
        PostgreSqlEventSourcingOptions? options)
        : this(NpgsqlDataSource.Create(connectionString), options, ownsDataSource: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlProjectionCheckpointStore" /> class.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    public PostgreSqlProjectionCheckpointStore(
        NpgsqlDataSource dataSource)
        : this(dataSource, options: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlProjectionCheckpointStore" /> class.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="options">The provider options.</param>
    public PostgreSqlProjectionCheckpointStore(
        NpgsqlDataSource dataSource,
        PostgreSqlEventSourcingOptions? options)
        : this(dataSource, options, ownsDataSource: false)
    {
    }

    private PostgreSqlProjectionCheckpointStore(
        NpgsqlDataSource dataSource,
        PostgreSqlEventSourcingOptions? options,
        bool ownsDataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        this.dataSource = dataSource;
        this.options = options ?? new PostgreSqlEventSourcingOptions();
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>
    /// Creates the provider schema and tables if they do not already exist.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    public ValueTask Initialize(CancellationToken ct) => PostgreSqlEventSourcingSchema.Initialize(dataSource, options, ct);

    /// <inheritdoc />
    public async ValueTask<ProjectionCheckpoint?> GetCheckpoint(string projectionName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionName);

        var schemaName = PostgreSqlNames.SchemaName(options);
        var schema = PostgreSqlNames.Schema(options);
        var start = Stopwatch.GetTimestamp();
        using var activity = StartCheckpointActivity(schemaName, projectionName, "get_checkpoint");
        try
        {
            var sql = $"""
                SELECT position
                FROM {schema}.projection_checkpoints
                WHERE projection_name = @projectionName;
                """;

            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("projectionName", projectionName);
            var result = await command.ExecuteScalarAsync(ct);

            var checkpoint = result is null or DBNull ? null : new ProjectionCheckpoint(projectionName, (long)result);
            if (checkpoint is not null)
            {
                activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagCheckpointPosition, checkpoint.Position);
            }

            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeSuccess);
            RecordCheckpointDuration(schemaName, "get_checkpoint", PostgreSqlEventSourcingTelemetry.OutcomeSuccess, Stopwatch.GetElapsedTime(start));

            return checkpoint;
        }
        catch (Exception exception) when (exception.ShouldHandleAsFailure(ct))
        {
            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeError);
            RecordCheckpointDuration(schemaName, "get_checkpoint", PostgreSqlEventSourcingTelemetry.OutcomeError, Stopwatch.GetElapsedTime(start));

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask Save(ProjectionCheckpoint checkpoint, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint.ProjectionName);
        ArgumentOutOfRangeException.ThrowIfNegative(checkpoint.Position);

        var schemaName = PostgreSqlNames.SchemaName(options);
        var schema = PostgreSqlNames.Schema(options);
        var start = Stopwatch.GetTimestamp();
        using var activity = StartCheckpointActivity(schemaName, checkpoint.ProjectionName, "save_checkpoint");
        activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagCheckpointPosition, checkpoint.Position);
        try
        {
            var sql = $"""
                INSERT INTO {schema}.projection_checkpoints (projection_name, position, updated_at_utc)
                VALUES (@projectionName, @position, @updatedAt)
                ON CONFLICT (projection_name)
                DO UPDATE SET position = GREATEST(projection_checkpoints.position, EXCLUDED.position),
                              updated_at_utc = EXCLUDED.updated_at_utc;
                """;

            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("projectionName", checkpoint.ProjectionName);
            command.Parameters.AddWithValue("position", checkpoint.Position);
            command.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);
            _ = await command.ExecuteNonQueryAsync(ct);

            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeSuccess);
            RecordCheckpointDuration(schemaName, "save_checkpoint", PostgreSqlEventSourcingTelemetry.OutcomeSuccess, Stopwatch.GetElapsedTime(start));
        }
        catch (Exception exception) when (exception.ShouldHandleAsFailure(ct))
        {
            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeError);
            RecordCheckpointDuration(schemaName, "save_checkpoint", PostgreSqlEventSourcingTelemetry.OutcomeError, Stopwatch.GetElapsedTime(start));

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (ownsDataSource)
        {
            await dataSource.DisposeAsync();
        }
    }

    private static Activity? StartCheckpointActivity(string schemaName, string projectionName, string operation)
    {
        var activity = PostgreSqlEventSourcingTelemetry.ActivitySource.StartActivity(
            PostgreSqlEventSourcingTelemetry.ActivityCheckpoint,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagSchema, schemaName);
        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagOperation, operation);
        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagProjectionName, projectionName);
        return activity;
    }

    private static void CompleteActivity(Activity? activity, string outcome)
    {
        activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagOutcome, outcome);
        activity?.SetStatus(
            string.Equals(outcome, PostgreSqlEventSourcingTelemetry.OutcomeError, StringComparison.Ordinal)
                ? ActivityStatusCode.Error
                : ActivityStatusCode.Ok);
    }

    private static void RecordCheckpointDuration(string schemaName, string operation, string outcome, TimeSpan duration)
    {
        PostgreSqlEventSourcingTelemetry.CheckpointDuration.Record(
            duration.TotalMilliseconds,
            new TagList
            {
                { PostgreSqlEventSourcingTelemetry.TagSchema, schemaName },
                { PostgreSqlEventSourcingTelemetry.TagOperation, operation },
                { PostgreSqlEventSourcingTelemetry.TagOutcome, outcome },
            });
    }
}
