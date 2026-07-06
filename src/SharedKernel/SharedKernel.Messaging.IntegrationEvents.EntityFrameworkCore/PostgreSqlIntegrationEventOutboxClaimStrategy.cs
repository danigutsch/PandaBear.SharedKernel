using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class PostgreSqlIntegrationEventOutboxClaimStrategy<TContext>
    : IIntegrationEventOutboxClaimStrategy<TContext>
    where TContext : DbContext
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public async ValueTask<IntegrationEventOutboxMessage[]> ClaimPending(
        TContext dbContext,
        int batchSize,
        DateTimeOffset now,
        string claimedBy,
        DateTimeOffset claimedUntil,
        CancellationToken ct)
    {
        if (!string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PostgreSQL outbox atomic claims require EF Core provider '{NpgsqlProviderName}'.");
        }

        var entityType = dbContext.Model.FindEntityType(typeof(IntegrationEventOutboxMessage))
            ?? throw new InvalidOperationException("Integration event outbox message entity is not configured.");
        var sql = CreateClaimSql(entityType);
        var command = FormattableStringFactory.Create(sql, claimedBy, claimedUntil, now, batchSize);

        return await dbContext.Set<IntegrationEventOutboxMessage>()
            .FromSql(command)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
    }

    internal static string CreateClaimSql(IEntityType entityType)
    {
        var storeObject = StoreObjectIdentifier.Table(
            entityType.GetTableName() ?? throw new InvalidOperationException("Outbox table name is not configured."),
            entityType.GetSchema());
        var tableName = FormatTableName(storeObject.Schema, storeObject.Name);
        var idColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventOutboxMessage.Id), storeObject));
        var publishedAtColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventOutboxMessage.PublishedAt), storeObject));
        var nextAttemptColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventOutboxMessage.NextPublishAttemptAt), storeObject));
        var claimedByColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventOutboxMessage.ClaimedBy), storeObject));
        var claimedUntilColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventOutboxMessage.ClaimedUntil), storeObject));
        var enqueuedAtColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventOutboxMessage.EnqueuedAt), storeObject));

        return $$"""
            WITH claimed AS (
                UPDATE {{tableName}} AS message
                SET {{claimedByColumn}} = {0}, {{claimedUntilColumn}} = {1}
                WHERE message.{{idColumn}} IN (
                    SELECT candidate.{{idColumn}}
                    FROM {{tableName}} AS candidate
                    WHERE candidate.{{publishedAtColumn}} IS NULL
                        AND (candidate.{{nextAttemptColumn}} IS NULL OR candidate.{{nextAttemptColumn}} <= {2})
                        AND (candidate.{{claimedUntilColumn}} IS NULL OR candidate.{{claimedUntilColumn}} <= {2})
                    ORDER BY candidate.{{enqueuedAtColumn}}
                    LIMIT {3}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *
            )
            SELECT *
            FROM claimed
            """;
    }

    private static string GetColumnName(IEntityType entityType, string propertyName, StoreObjectIdentifier storeObject) =>
        entityType.FindProperty(propertyName)?.GetColumnName(storeObject)
        ?? throw new InvalidOperationException($"Outbox column for '{propertyName}' is not configured.");

    private static string FormatTableName(string? schema, string table) => schema is null
        ? FormatIdentifier(table)
        : $"{FormatIdentifier(schema)}.{FormatIdentifier(table)}";

    private static string FormatIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
