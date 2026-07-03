using Npgsql;

namespace SharedKernel.EventSourcing.Npgsql;

/// <summary>
/// Creates PostgreSQL tables required by the event-sourcing provider.
/// </summary>
public static class PostgreSqlEventSourcingSchema
{
    /// <summary>
    /// Creates the provider schema and tables if they do not already exist.
    /// </summary>
    /// <param name="dataSource">The PostgreSQL data source.</param>
    /// <param name="options">The provider options.</param>
    /// <param name="ct">The cancellation token.</param>
    public static async ValueTask Initialize(
        NpgsqlDataSource dataSource,
        PostgreSqlEventSourcingOptions? options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var schema = PostgreSqlNames.Schema(options);
        var sql = $"""
            CREATE SCHEMA IF NOT EXISTS {schema};

            CREATE TABLE IF NOT EXISTS {schema}.event_streams (
                stream_id text PRIMARY KEY,
                stream_revision bigint NOT NULL,
                created_at_utc timestamp with time zone NOT NULL,
                updated_at_utc timestamp with time zone NOT NULL
            );

            CREATE TABLE IF NOT EXISTS {schema}.events (
                position bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
                event_id uuid PRIMARY KEY,
                stream_id text NOT NULL REFERENCES {schema}.event_streams(stream_id) ON DELETE RESTRICT,
                stream_revision bigint NOT NULL,
                event_type text NOT NULL,
                payload_json jsonb NOT NULL,
                recorded_at_utc timestamp with time zone NOT NULL,
                CONSTRAINT uq_events_stream_revision UNIQUE (stream_id, stream_revision)
            );

            CREATE TABLE IF NOT EXISTS {schema}.projection_checkpoints (
                projection_name text PRIMARY KEY,
                position bigint NOT NULL,
                updated_at_utc timestamp with time zone NOT NULL
            );
            """;

        await using var command = dataSource.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(ct);
    }
}
