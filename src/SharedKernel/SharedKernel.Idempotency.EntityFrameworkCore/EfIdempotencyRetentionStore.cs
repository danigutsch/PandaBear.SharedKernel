using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace PandaBear.Idempotency.EntityFrameworkCore;

/// <summary>
/// Purges expired idempotency entries from the table of a DbContext one batch at a time.
/// A batch selects its rows with FOR UPDATE SKIP LOCKED, so concurrent workers never wait on or delete the same entry, and the rows stay locked until the delete.
/// Selection, the optional guard call and the delete run in the transaction of the context (or in one of their own when none is open),
/// so a caller that checks holds in that transaction decides and deletes atomically.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the idempotency table.</typeparam>
internal sealed class EfIdempotencyRetentionStore<TContext>(TContext dbContext) : IIdempotencyRetentionStore
    where TContext : DbContext
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly HashSet<string> NoOwners = [];

    /// <inheritdoc />
    public ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        CancellationToken ct,
        string? scopePrefix = null) =>
        Purge(cutoff, ownerTrackedScopePrefix, heldRetentionOwners, batchSize, guard: null, scopePrefix, ct);

    /// <inheritdoc />
    public ValueTask<IdempotencyRetentionPurgeResult> PurgeExpired(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        Func<IReadOnlyCollection<string>, CancellationToken, ValueTask<IReadOnlySet<string>>> guard,
        CancellationToken ct,
        string? scopePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(guard);
        return Purge(cutoff, ownerTrackedScopePrefix, heldRetentionOwners, batchSize, guard, scopePrefix, ct);
    }

    private async ValueTask<IdempotencyRetentionPurgeResult> Purge(
        DateTimeOffset cutoff,
        string? ownerTrackedScopePrefix,
        IReadOnlyCollection<string> heldRetentionOwners,
        int batchSize,
        Func<IReadOnlyCollection<string>, CancellationToken, ValueTask<IReadOnlySet<string>>>? guard,
        string? scopePrefix,
        CancellationToken ct)
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
        var sql = PurgeSql.For(entityType);
        var parameters = new PurgeParameters(cutoff.ToUniversalTime(), held, ownerTrackedScopePrefix, scopePrefix, batchSize);
        var connectionWasOpen = dbContext.Database.GetDbConnection().State is System.Data.ConnectionState.Open;
        await dbContext.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            var ambient = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            if (ambient is not null)
            {
                return await PurgeBatch(sql, parameters, guard, ambient, ct).ConfigureAwait(false);
            }

            var own = await dbContext.Database.GetDbConnection().BeginTransactionAsync(ct).ConfigureAwait(false);
            await using (own.ConfigureAwait(false))
            {
                var result = await PurgeBatch(sql, parameters, guard, own, ct).ConfigureAwait(false);
                await own.CommitAsync(ct).ConfigureAwait(false);
                return result;
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

    private async ValueTask<IdempotencyRetentionPurgeResult> PurgeBatch(
        PurgeSql sql,
        PurgeParameters parameters,
        Func<IReadOnlyCollection<string>, CancellationToken, ValueTask<IReadOnlySet<string>>>? guard,
        DbTransaction transaction,
        CancellationToken ct)
    {
        var selected = new List<SelectedEntry>();
        var select = CreateCommand(sql.Select, transaction, parameters);
        await using (select.ConfigureAwait(false))
        {
            var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    selected.Add(new SelectedEntry(
                        reader.GetString(0),
                        reader.GetString(1),
                        await reader.IsDBNullAsync(2, ct).ConfigureAwait(false) ? null : reader.GetString(2)));
                }
            }
        }

        var owners = selected
            .Select(static entry => entry.Owner)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        IReadOnlySet<string> guardHeld = NoOwners;
        if (guard is not null && owners.Length > 0)
        {
            guardHeld = await guard(owners, ct).ConfigureAwait(false) ?? NoOwners;
        }

        var guardHeldOwners = owners.Where(guardHeld.Contains).ToArray();
        var guardHeldRows = selected.Count(entry => entry.Owner is not null && guardHeld.Contains(entry.Owner));

        long purged = 0;
        if (selected.Count > guardHeldRows)
        {
            var delete = CreateCommand(sql.Delete, transaction, parameters);
            await using var _ = delete.ConfigureAwait(false);
            AddParameter(delete, "scopes", selected.Select(static entry => entry.Scope).ToArray());
            AddParameter(delete, "keys", selected.Select(static entry => entry.Key).ToArray());
            AddParameter(delete, "guardHeld", guardHeldOwners);
            purged = await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        var count = CreateCommand(sql.Count, transaction, parameters);
        await using var __ = count.ConfigureAwait(false);
        var counts = await count.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await using (counts.ConfigureAwait(false))
        {
            await counts.ReadAsync(ct).ConfigureAwait(false);
            return new IdempotencyRetentionPurgeResult(purged, counts.GetInt64(0) + guardHeldRows, counts.GetInt64(1));
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The statements are built only from quoted table and column names of the mapped model; every caller value travels as a parameter.")]
    private DbCommand CreateCommand(string commandText, DbTransaction transaction, PurgeParameters parameters)
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        command.Transaction = transaction;
        command.CommandTimeout = dbContext.Database.GetCommandTimeout() ?? command.CommandTimeout;
        AddParameter(command, "cutoff", parameters.Cutoff);
        AddParameter(command, "held", parameters.Held);
        AddParameter(command, "tracked", parameters.TrackedPrefix is not null);
        AddParameter(command, "prefix", parameters.TrackedPrefix ?? string.Empty);
        AddParameter(command, "restricted", parameters.ScopePrefix is not null);
        AddParameter(command, "scopePrefix", parameters.ScopePrefix ?? string.Empty);
        AddParameter(command, "batch", parameters.BatchSize);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record PurgeParameters(
        DateTimeOffset Cutoff,
        string[] Held,
        string? TrackedPrefix,
        string? ScopePrefix,
        int BatchSize);

    private sealed record SelectedEntry(string Scope, string Key, string? Owner);

    /// <summary>The select, delete and count statements built from the mapped table and column names.</summary>
    private sealed record PurgeSql(string Select, string Delete, string Count)
    {
        /// <summary>Builds the statements using the column and table names of the mapped idempotency entity.</summary>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="entityType"/> is not mapped to a table.</exception>
        public static PurgeSql For(IEntityType entityType)
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

            return new PurgeSql(
                $"""
                SELECT entry.{scope}, entry.{key}, entry.{owner}
                FROM {table} AS entry
                WHERE {candidate}
                    AND NOT COALESCE({isHeld}, FALSE)
                    AND NOT ({isUnattributed})
                ORDER BY {touched}, entry.{scope}, entry.{key}
                LIMIT @batch
                FOR UPDATE SKIP LOCKED
                """,
                $"""
                DELETE FROM {table} AS target
                USING unnest(@scopes, @keys) AS selected(purge_scope, purge_key)
                WHERE target.{scope} = selected.purge_scope
                    AND target.{key} = selected.purge_key
                    AND NOT COALESCE(target.{owner} = ANY(@guardHeld), FALSE)
                """,
                $"""
                SELECT
                    (SELECT COUNT(*) FROM {table} AS entry WHERE {candidate} AND {isHeld}) AS "Held",
                    (SELECT COUNT(*) FROM {table} AS entry WHERE {candidate} AND {isUnattributed}) AS "Unattributed"
                """);
        }
    }

    private static string GetColumnName(IEntityType entityType, string propertyName, StoreObjectIdentifier storeObject) =>
        entityType.FindProperty(propertyName)?.GetColumnName(storeObject)
        ?? throw new InvalidOperationException($"Idempotency column for '{propertyName}' is not configured.");

    private static string FormatTableName(string? schema, string table) => schema is null
        ? FormatIdentifier(table)
        : $"{FormatIdentifier(schema)}.{FormatIdentifier(table)}";

    private static string FormatIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
