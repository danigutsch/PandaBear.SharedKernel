using Npgsql;
using NpgsqlTypes;

namespace SharedKernel.Npgsql;

/// <summary>Reads bounded PostgreSQL integrity reports for separately stored binary artifacts.</summary>
public static class PostgreSqlArtifactIntegrity
{
    private const int MaxPageSize = 100;
    private const int CommandTimeoutSeconds = 30;

    /// <summary>Reads one read-only page in PostgreSQL UUID order, hashing bodies on the server.</summary>
    /// <param name="dataSource">The caller-owned PostgreSQL data source.</param>
    /// <param name="mapping">The identifiers of the metadata and body relations.</param>
    /// <param name="afterId">The exclusive cursor, or null to start at the first UUID.</param>
    /// <param name="pageSize">The maximum number of rows to return, from 1 to 100.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The opaque identifier and status for each selected metadata row.</returns>
    /// <exception cref="ArgumentNullException">The data source or mapping is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page size is outside the supported range.</exception>
    /// <exception cref="ArgumentException">A mapped identifier is null, empty, or contains a NUL character.</exception>
    public static async Task<IReadOnlyList<PostgreSqlArtifactIntegrityResult>> ReadPage(
        NpgsqlDataSource dataSource,
        PostgreSqlArtifactIntegrityMapping mapping,
        Guid? afterId,
        int pageSize,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
        ct.ThrowIfCancellationRequested();

        var query = new NpgsqlBatchCommand(BuildQuery(mapping));
        var cursor = afterId is Guid id ? (object)id : DBNull.Value;
        query.Parameters.AddWithValue("after_id", NpgsqlDbType.Uuid, cursor);
        query.Parameters.AddWithValue("page_size", NpgsqlDbType.Integer, pageSize);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var batch = new NpgsqlBatch(connection, transaction) { Timeout = CommandTimeoutSeconds };
        batch.BatchCommands.Add(new NpgsqlBatchCommand("SET TRANSACTION READ ONLY"));
        batch.BatchCommands.Add(new NpgsqlBatchCommand("SET LOCAL statement_timeout = '30s'"));
        batch.BatchCommands.Add(query);

        await using var reader = await batch.ExecuteReaderAsync(ct);
        var results = new List<PostgreSqlArtifactIntegrityResult>(pageSize);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new PostgreSqlArtifactIntegrityResult(reader.GetGuid(0), (PostgreSqlArtifactIntegrityStatus)reader.GetInt32(1)));
        }

        return results;
    }

    private static string BuildQuery(PostgreSqlArtifactIntegrityMapping mapping)
    {
        using var builder = new NpgsqlCommandBuilder();
        var id = Quote(builder, mapping.IdColumn);
        var bodyId = Quote(builder, mapping.BodyIdColumn);
        var content = Quote(builder, mapping.BodyContentColumn);

        return $$"""
            WITH page AS MATERIALIZED (
                SELECT metadata.{{id}} AS id,
                    metadata.{{Quote(builder, mapping.LengthColumn)}} AS recorded_length,
                    metadata.{{Quote(builder, mapping.Sha256Column)}} AS recorded_digest
                FROM {{Quote(builder, mapping.MetadataSchema)}}.{{Quote(builder, mapping.MetadataTable)}} AS metadata
                WHERE metadata.{{Quote(builder, mapping.EligibilityColumn)}} IS NOT NULL
                    AND (@after_id IS NULL OR metadata.{{id}} > @after_id)
                ORDER BY metadata.{{id}}
                LIMIT @page_size
            )
            SELECT page.id,
                CASE
                    WHEN body.{{bodyId}} IS NULL OR body.{{content}} IS NULL
                        THEN {{(int)PostgreSqlArtifactIntegrityStatus.MissingBody}}
                    WHEN page.recorded_length IS NULL OR page.recorded_digest IS NULL
                        THEN {{(int)PostgreSqlArtifactIntegrityStatus.MissingIntegrityMetadata}}
                    WHEN page.recorded_length <= 0 OR page.recorded_length <> pg_catalog.octet_length(body.{{content}})
                        THEN {{(int)PostgreSqlArtifactIntegrityStatus.LengthMismatch}}
                    WHEN (page.recorded_digest COLLATE pg_catalog."C") <> pg_catalog.encode(pg_catalog.sha256(body.{{content}}), 'hex')
                        THEN {{(int)PostgreSqlArtifactIntegrityStatus.DigestMismatch}}
                    ELSE {{(int)PostgreSqlArtifactIntegrityStatus.Intact}}
                END AS status
            FROM page
            LEFT JOIN {{Quote(builder, mapping.BodySchema)}}.{{Quote(builder, mapping.BodyTable)}} AS body
                ON body.{{bodyId}} = page.id
            ORDER BY page.id
            """;
    }

    private static string Quote(NpgsqlCommandBuilder builder, string identifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (identifier.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("A PostgreSQL identifier cannot contain a NUL character.", nameof(identifier));
        }

        return builder.QuoteIdentifier(identifier);
    }
}
