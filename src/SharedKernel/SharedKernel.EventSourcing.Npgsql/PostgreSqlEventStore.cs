using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using SharedKernel.BuildingBlocks;
using SharedKernel.Npgsql;

namespace SharedKernel.EventSourcing.Npgsql;

/// <summary>
/// Persists event streams in PostgreSQL.
/// </summary>
public sealed class PostgreSqlEventStore : IEventStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly IEventSerializer serializer;
    private readonly PostgreSqlEventSourcingOptions options;
    private readonly bool ownsDataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlEventStore" /> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="serializer">The event serializer.</param>
    public PostgreSqlEventStore(
        string connectionString,
        IEventSerializer serializer)
        : this(connectionString, serializer, options: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlEventStore" /> class.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="serializer">The event serializer.</param>
    /// <param name="options">The provider options.</param>
    public PostgreSqlEventStore(
        string connectionString,
        IEventSerializer serializer,
        PostgreSqlEventSourcingOptions? options)
        : this(NpgsqlDataSource.Create(connectionString), serializer, options, ownsDataSource: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlEventStore" /> class.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="serializer">The event serializer.</param>
    public PostgreSqlEventStore(
        NpgsqlDataSource dataSource,
        IEventSerializer serializer)
        : this(dataSource, serializer, options: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlEventStore" /> class.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="serializer">The event serializer.</param>
    /// <param name="options">The provider options.</param>
    public PostgreSqlEventStore(
        NpgsqlDataSource dataSource,
        IEventSerializer serializer,
        PostgreSqlEventSourcingOptions? options)
        : this(dataSource, serializer, options, ownsDataSource: false)
    {
    }

    private PostgreSqlEventStore(
        NpgsqlDataSource dataSource,
        IEventSerializer serializer,
        PostgreSqlEventSourcingOptions? options,
        bool ownsDataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(serializer);

        this.dataSource = dataSource;
        this.serializer = serializer;
        this.options = options ?? new PostgreSqlEventSourcingOptions();
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>
    /// Creates the provider schema and tables if they do not already exist.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    public ValueTask Initialize(CancellationToken ct) => PostgreSqlEventSourcingSchema.Initialize(dataSource, options, ct);

    /// <inheritdoc />
    public async ValueTask Append(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        IReadOnlyCollection<object> events,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        var schemaName = PostgreSqlNames.SchemaName(options);
        var schema = PostgreSqlNames.Schema(options);
        var appendLockKey = GetAppendLockKey(schemaName);
        var start = Stopwatch.GetTimestamp();
        using var activity = StartAppendActivity(schemaName, expectedRevision, events.Count);
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await PostgreSqlTransactionAdvisoryLock.Acquire(connection, transaction, appendLockKey, ct);

            var currentRevision = await GetCurrentRevision(connection, transaction, schema, streamId, ct);
            if (currentRevision is null)
            {
                await CreateStream(connection, transaction, schema, streamId, ct);
                currentRevision = await GetCurrentRevision(connection, transaction, schema, streamId, ct)
                    ?? throw new InvalidOperationException("Event stream row could not be created or loaded.");
            }

            EnsureExpectedRevision(streamId, expectedRevision, currentRevision);

            var recordedAt = DateTimeOffset.UtcNow;
            foreach (var eventData in events)
            {
                ArgumentNullException.ThrowIfNull(eventData);

                currentRevision++;
                await AppendEvent(connection, transaction, schema, streamId, currentRevision.Value, eventData, recordedAt, ct);
            }

            await UpdateStreamRevision(connection, transaction, schema, streamId, currentRevision.Value, recordedAt, ct);
            await transaction.CommitAsync(ct);
            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeSuccess);
            RecordAppendDuration(schemaName, PostgreSqlEventSourcingTelemetry.OutcomeSuccess, Stopwatch.GetElapsedTime(start));
            RecordEventsAppended(schemaName, events.Count);
        }
        catch (ExpectedStreamRevisionConflictException exception)
        {
            CompleteConflictActivity(activity, exception.ActualRevision);
            RecordAppendDuration(schemaName, PostgreSqlEventSourcingTelemetry.OutcomeConflict, Stopwatch.GetElapsedTime(start));
            RecordAppendConflict(schemaName);

            throw;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            var actualRevision = await GetCurrentRevision(schema, streamId, ct);
            CompleteConflictActivity(activity, actualRevision);
            RecordAppendDuration(schemaName, PostgreSqlEventSourcingTelemetry.OutcomeConflict, Stopwatch.GetElapsedTime(start));
            RecordAppendConflict(schemaName);

            throw new ExpectedStreamRevisionConflictException(streamId, expectedRevision, actualRevision);
        }
        catch (Exception exception) when (exception.ShouldHandleAsFailure(ct))
        {
            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeError);
            RecordAppendDuration(schemaName, PostgreSqlEventSourcingTelemetry.OutcomeError, Stopwatch.GetElapsedTime(start));

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<EventEnvelope>> Load(
        StreamId streamId,
        StreamRevision? afterRevision,
        CancellationToken ct)
    {
        var schemaName = PostgreSqlNames.SchemaName(options);
        var schema = PostgreSqlNames.Schema(options);
        var start = Stopwatch.GetTimestamp();
        using var activity = StartLoadActivity(schemaName, "load");
        try
        {
            var sql = $"""
                SELECT position, stream_revision, event_id, event_type, payload_json::text, recorded_at_utc
                FROM {schema}.events
                WHERE stream_id = @streamId
                  AND (@afterRevision IS NULL OR stream_revision > @afterRevision)
                ORDER BY stream_revision;
                """;

            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("streamId", streamId.Value);
            var afterRevisionParameter = command.Parameters.Add("afterRevision", NpgsqlDbType.Bigint);
            afterRevisionParameter.Value = afterRevision is null ? DBNull.Value : afterRevision.Value.Value;

            var envelopes = new List<EventEnvelope>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var position = reader.GetInt64(0);
                var revision = StreamRevision.From(reader.GetInt64(1));
                var eventId = reader.GetGuid(2);
                var eventType = reader.GetString(3);
                var payloadJson = reader.GetString(4);
                var recordedAt = await reader.GetFieldValueAsync<DateTimeOffset>(5, ct);
                var eventData = serializer.Deserialize(eventType, payloadJson);

                envelopes.Add(new EventEnvelope(streamId, position, revision, eventId, eventType, eventData, recordedAt));
            }

            CompleteLoadActivity(activity, envelopes.Count);
            RecordLoadDuration(schemaName, "load", PostgreSqlEventSourcingTelemetry.OutcomeSuccess, Stopwatch.GetElapsedTime(start));
            RecordEventsLoaded(schemaName, "load", envelopes.Count);

            return envelopes;
        }
        catch (Exception exception) when (exception.ShouldHandleAsFailure(ct))
        {
            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeError);
            RecordLoadDuration(schemaName, "load", PostgreSqlEventSourcingTelemetry.OutcomeError, Stopwatch.GetElapsedTime(start));

            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<EventEnvelope>> LoadAfter(
        long position,
        int maxCount,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        var schemaName = PostgreSqlNames.SchemaName(options);
        var schema = PostgreSqlNames.Schema(options);
        var start = Stopwatch.GetTimestamp();
        using var activity = StartLoadActivity(schemaName, "load_after");
        try
        {
            var sql = $"""
                SELECT position, stream_id, stream_revision, event_id, event_type, payload_json::text, recorded_at_utc
                FROM {schema}.events
                WHERE position > @position
                ORDER BY position
                LIMIT @maxCount;
                """;

            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("position", position);
            command.Parameters.AddWithValue("maxCount", maxCount);

            var envelopes = new List<EventEnvelope>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var eventPosition = reader.GetInt64(0);
                var streamId = StreamId.From(reader.GetString(1));
                var revision = StreamRevision.From(reader.GetInt64(2));
                var eventId = reader.GetGuid(3);
                var eventType = reader.GetString(4);
                var payloadJson = reader.GetString(5);
                var recordedAt = await reader.GetFieldValueAsync<DateTimeOffset>(6, ct);
                var eventData = serializer.Deserialize(eventType, payloadJson);

                envelopes.Add(new EventEnvelope(streamId, eventPosition, revision, eventId, eventType, eventData, recordedAt));
            }

            CompleteLoadActivity(activity, envelopes.Count);
            RecordLoadDuration(schemaName, "load_after", PostgreSqlEventSourcingTelemetry.OutcomeSuccess, Stopwatch.GetElapsedTime(start));
            RecordEventsLoaded(schemaName, "load_after", envelopes.Count);

            return envelopes;
        }
        catch (Exception exception) when (exception.ShouldHandleAsFailure(ct))
        {
            CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeError);
            RecordLoadDuration(schemaName, "load_after", PostgreSqlEventSourcingTelemetry.OutcomeError, Stopwatch.GetElapsedTime(start));

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

    private static async ValueTask<long?> GetCurrentRevision(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        StreamId streamId,
        CancellationToken ct)
    {
        var sql = $"""
            SELECT stream_revision
            FROM {schema}.event_streams
            WHERE stream_id = @streamId
            FOR UPDATE;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("streamId", streamId.Value);
        var result = await command.ExecuteScalarAsync(ct);

        return result is null or DBNull ? null : (long)result;
    }

    private async ValueTask<StreamRevision?> GetCurrentRevision(
        string schema,
        StreamId streamId,
        CancellationToken ct)
    {
        var sql = $"""
            SELECT stream_revision
            FROM {schema}.event_streams
            WHERE stream_id = @streamId;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("streamId", streamId.Value);
        var result = await command.ExecuteScalarAsync(ct);

        return result is null or DBNull ? null : ToStreamRevision((long)result);
    }

    private static void EnsureExpectedRevision(
        StreamId streamId,
        ExpectedStreamRevision expectedRevision,
        long? currentRevision)
    {
        if (!expectedRevision.RequiresEmptyStream && expectedRevision.Value is null)
        {
            return;
        }

        if (expectedRevision.RequiresEmptyStream && currentRevision is null or 0)
        {
            return;
        }

        if (expectedRevision.Value == currentRevision)
        {
            return;
        }

        var actualRevision = currentRevision is > 0 ? StreamRevision.From(currentRevision.Value) : default(StreamRevision?);
        throw new ExpectedStreamRevisionConflictException(streamId, expectedRevision, actualRevision);
    }

    private static StreamRevision? ToStreamRevision(long value) => value > 0 ? StreamRevision.From(value) : default(StreamRevision?);

    private static Activity? StartAppendActivity(
        string schemaName,
        ExpectedStreamRevision expectedRevision,
        int eventCount)
    {
        var activity = PostgreSqlEventSourcingTelemetry.ActivitySource.StartActivity(
            PostgreSqlEventSourcingTelemetry.ActivityAppend,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagSchema, schemaName);
        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagOperation, "append");
        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagEventCount, eventCount);
        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagExpectedRevisionMode, GetExpectedRevisionMode(expectedRevision));
        if (expectedRevision.Value is long revision)
        {
            activity.SetTag(PostgreSqlEventSourcingTelemetry.TagExpectedRevisionValue, revision);
        }

        return activity;
    }

    private static Activity? StartLoadActivity(string schemaName, string operation)
    {
        var activity = PostgreSqlEventSourcingTelemetry.ActivitySource.StartActivity(
            PostgreSqlEventSourcingTelemetry.ActivityLoad,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagSchema, schemaName);
        activity.SetTag(PostgreSqlEventSourcingTelemetry.TagOperation, operation);
        return activity;
    }

    private static void CompleteLoadActivity(Activity? activity, int eventCount)
    {
        activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagEventCount, eventCount);
        CompleteActivity(activity, PostgreSqlEventSourcingTelemetry.OutcomeSuccess);
    }

    private static void CompleteConflictActivity(Activity? activity, StreamRevision? actualRevision)
    {
        activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagOutcome, PostgreSqlEventSourcingTelemetry.OutcomeConflict);
        if (actualRevision is StreamRevision revision)
        {
            activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagActualRevision, revision.Value);
        }

        activity?.SetStatus(ActivityStatusCode.Error, PostgreSqlEventSourcingTelemetry.OutcomeConflict);
    }

    private static void CompleteActivity(Activity? activity, string outcome)
    {
        activity?.SetTag(PostgreSqlEventSourcingTelemetry.TagOutcome, outcome);
        activity?.SetStatus(
            string.Equals(outcome, PostgreSqlEventSourcingTelemetry.OutcomeError, StringComparison.Ordinal)
                ? ActivityStatusCode.Error
                : ActivityStatusCode.Ok);
    }

    private static string GetExpectedRevisionMode(ExpectedStreamRevision expectedRevision)
    {
        if (expectedRevision.RequiresEmptyStream)
        {
            return PostgreSqlEventSourcingTelemetry.ExpectedRevisionNoStream;
        }

        return expectedRevision.Value is null
            ? PostgreSqlEventSourcingTelemetry.ExpectedRevisionAny
            : PostgreSqlEventSourcingTelemetry.ExpectedRevisionExact;
    }

    private static void RecordAppendDuration(string schemaName, string outcome, TimeSpan duration)
    {
        PostgreSqlEventSourcingTelemetry.AppendDuration.Record(
            duration.TotalMilliseconds,
            new TagList
            {
                { PostgreSqlEventSourcingTelemetry.TagSchema, schemaName },
                { PostgreSqlEventSourcingTelemetry.TagOutcome, outcome },
            });
    }

    private static void RecordLoadDuration(string schemaName, string operation, string outcome, TimeSpan duration)
    {
        PostgreSqlEventSourcingTelemetry.LoadDuration.Record(
            duration.TotalMilliseconds,
            new TagList
            {
                { PostgreSqlEventSourcingTelemetry.TagSchema, schemaName },
                { PostgreSqlEventSourcingTelemetry.TagOperation, operation },
                { PostgreSqlEventSourcingTelemetry.TagOutcome, outcome },
            });
    }

    private static void RecordEventsAppended(string schemaName, int eventCount)
    {
        PostgreSqlEventSourcingTelemetry.EventsAppended.Add(
            eventCount,
            new TagList
            {
                { PostgreSqlEventSourcingTelemetry.TagSchema, schemaName },
            });
    }

    private static void RecordEventsLoaded(string schemaName, string operation, int eventCount)
    {
        PostgreSqlEventSourcingTelemetry.EventsLoaded.Add(
            eventCount,
            new TagList
            {
                { PostgreSqlEventSourcingTelemetry.TagSchema, schemaName },
                { PostgreSqlEventSourcingTelemetry.TagOperation, operation },
            });
    }

    private static void RecordAppendConflict(string schemaName)
    {
        PostgreSqlEventSourcingTelemetry.AppendConflicts.Add(
            1,
            new TagList
            {
                { PostgreSqlEventSourcingTelemetry.TagSchema, schemaName },
            });
    }

    private static long GetAppendLockKey(string schemaName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"SharedKernel.EventSourcing.Npgsql:{schemaName}"));
        return BinaryPrimitives.ReadInt64LittleEndian(hash);
    }

    private static async ValueTask CreateStream(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        StreamId streamId,
        CancellationToken ct)
    {
        var sql = $"""
            INSERT INTO {schema}.event_streams (stream_id, stream_revision, created_at_utc, updated_at_utc)
            VALUES (@streamId, 0, @recordedAt, @recordedAt)
            ON CONFLICT (stream_id) DO NOTHING;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("streamId", streamId.Value);
        command.Parameters.AddWithValue("recordedAt", DateTimeOffset.UtcNow);
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    private async ValueTask AppendEvent(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        StreamId streamId,
        long revision,
        object eventData,
        DateTimeOffset recordedAt,
        CancellationToken ct)
    {
        var sql = $"""
            INSERT INTO {schema}.events (event_id, stream_id, stream_revision, event_type, payload_json, recorded_at_utc)
            VALUES (@eventId, @streamId, @streamRevision, @eventType, @payloadJson, @recordedAt);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("eventId", Guid.CreateVersion7());
        command.Parameters.AddWithValue("streamId", streamId.Value);
        command.Parameters.AddWithValue("streamRevision", revision);
        command.Parameters.AddWithValue("eventType", serializer.GetEventType(eventData));
        command.Parameters.AddWithValue("payloadJson", NpgsqlDbType.Jsonb, serializer.Serialize(eventData));
        command.Parameters.AddWithValue("recordedAt", recordedAt);
        _ = await command.ExecuteNonQueryAsync(ct);
    }

    private static async ValueTask UpdateStreamRevision(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string schema,
        StreamId streamId,
        long revision,
        DateTimeOffset recordedAt,
        CancellationToken ct)
    {
        var sql = $"""
            UPDATE {schema}.event_streams
            SET stream_revision = @streamRevision,
                updated_at_utc = @recordedAt
            WHERE stream_id = @streamId;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("streamId", streamId.Value);
        command.Parameters.AddWithValue("streamRevision", revision);
        command.Parameters.AddWithValue("recordedAt", recordedAt);
        _ = await command.ExecuteNonQueryAsync(ct);
    }
}
