using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace PandaBear.Idempotency.EntityFrameworkCore;

/// <summary>
/// Purges expired idempotency entries from the table of a DbContext with one atomic PostgreSQL statement per batch.
/// A batch skips rows that another worker has locked, so concurrent workers never wait on or delete the same entry.
/// The statement runs in the transaction of the context, so a caller that checks holds in that transaction decides and deletes atomically.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the idempotency table.</typeparam>
internal sealed class EfIdempotencyRetentionStore<TContext>(TContext dbContext) : IIdempotencyRetentionStore
    where TContext : DbContext
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <inheritdoc />
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The statement is built only from quoted table and column names of the mapped model; every caller value travels as a parameter.")]
    public async ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        CancellationToken ct,
        string? scopePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(heldRetentionOwners);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (ownerTrackedScopePrefix is { Length: 0 })
        {
            throw new ArgumentException("The owner-tracked scope prefix cannot be empty.", nameof(ownerTrackedScopePrefix));
        }

        if (scopePrefix is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scopePrefix);
        }

        var held = heldRetentionOwners
            .Select(owner => RetentionOwnerIdentifier.Require(owner, nameof(heldRetentionOwners)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Idempotency retention purges require EF Core provider '{NpgsqlProviderName}'.");
        }

        var entityType = dbContext.Model.FindEntityType(typeof(IdempotencyEntryEntity))
            ?? throw new InvalidOperationException("Idempotency entry entity is not configured.");
        var connectionWasOpen = dbContext.Database.GetDbConnection().State is System.Data.ConnectionState.Open;
        await dbContext.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var command = dbContext.Database.GetDbConnection().CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = CreatePurgeSql(entityType);
                command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandTimeout = dbContext.Database.GetCommandTimeout() ?? command.CommandTimeout;
                AddParameter(command, "cutoff", cutoff.ToUniversalTime());
                AddParameter(command, "held", held);
                AddParameter(command, "tracked", ownerTrackedScopePrefix is not null);
                AddParameter(command, "prefix", ownerTrackedScopePrefix ?? string.Empty);
                AddParameter(command, "restricted", scopePrefix is not null);
                AddParameter(command, "scopePrefix", scopePrefix ?? string.Empty);
                AddParameter(command, "batch", batchSize);

                var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    await reader.ReadAsync(ct).ConfigureAwait(false);
                    return new IdempotencyRetentionPurgeResult(
                        reader.GetInt64(0),
                        reader.GetInt64(1),
                        reader.GetInt64(2));
                }
            }
        }
        finally
        {
            if (!connectionWasOpen)
            {
                await dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>Builds the purge statement using the column and table names of the mapped idempotency entity.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="entityType"/> is not mapped to a table.</exception>
    internal static string CreatePurgeSql(IEntityType entityType)
    {
        var storeObject = StoreObjectIdentifier.Table(
            entityType.GetTableName() ?? throw new InvalidOperationException("Idempotency table name is not configured."),
            entityType.GetSchema());
        var table = FormatTableName(storeObject.Schema, storeObject.Name);
        var scope = FormatIdentifier(GetColumnName(entityType, nameof(IdempotencyEntryEntity.Scope), storeObject));
        var key = FormatIdentifier(GetColumnName(entityType, nameof(IdempotencyEntryEntity.Key), storeObject));
        var startedAt = FormatIdentifier(GetColumnName(entityType, nameof(IdempotencyEntryEntity.StartedAt), storeObject));
        var completedAt = FormatIdentifier(GetColumnName(entityType, nameof(IdempotencyEntryEntity.CompletedAt), storeObject));
        var owner = FormatIdentifier(GetColumnName(entityType, nameof(IdempotencyEntryEntity.RetentionOwner), storeObject));

        var touched = $"COALESCE(entry.{completedAt}, entry.{startedAt})";
        var inScope = $"(NOT @restricted OR starts_with(entry.{scope}, @scopePrefix))";
        var candidate = $"{touched} < @cutoff AND {inScope}";
        var isHeld = $"entry.{owner} = ANY(@held)";
        var isUnattributed = $"entry.{owner} IS NULL AND @tracked AND starts_with(entry.{scope}, @prefix)";

        return $$"""
            WITH purgeable AS (
                SELECT entry.{{scope}} AS purge_scope, entry.{{key}} AS purge_key
                FROM {{table}} AS entry
                WHERE {{candidate}}
                    AND NOT COALESCE({{isHeld}}, FALSE)
                    AND NOT ({{isUnattributed}})
                ORDER BY {{touched}}, entry.{{scope}}, entry.{{key}}
                LIMIT @batch
                FOR UPDATE SKIP LOCKED
            ),
            purged AS (
                DELETE FROM {{table}} AS target
                USING purgeable
                WHERE target.{{scope}} = purgeable.purge_scope AND target.{{key}} = purgeable.purge_key
                RETURNING 1
            )
            SELECT
                (SELECT COUNT(*) FROM purged) AS "Purged",
                (SELECT COUNT(*) FROM {{table}} AS entry WHERE {{candidate}} AND {{isHeld}}) AS "Held",
                (SELECT COUNT(*) FROM {{table}} AS entry WHERE {{candidate}} AND {{isUnattributed}}) AS "Unattributed"
            """;
    }

    private static string GetColumnName(IEntityType entityType, string propertyName, StoreObjectIdentifier storeObject) =>
        entityType.FindProperty(propertyName)?.GetColumnName(storeObject)
        ?? throw new InvalidOperationException($"Idempotency column for '{propertyName}' is not configured.");

    private static string FormatTableName(string? schema, string table) => schema is null
        ? FormatIdentifier(table)
        : $"{FormatIdentifier(schema)}.{FormatIdentifier(table)}";

    private static string FormatIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
