using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal static class PostgreSqlIntegrationEventTransportClaimSql
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    public static async ValueTask<IntegrationEventTransportMessage[]> ClaimPending<TContext>(
        TContext dbContext,
        string consumerName,
        int batchSize,
        DateTimeOffset now,
        string claimedBy,
        DateTimeOffset claimedUntil,
        CancellationToken ct)
        where TContext : DbContext
    {
        if (!string.Equals(dbContext.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"PostgreSQL transport claims require EF Core provider '{NpgsqlProviderName}'.");
        }

        var entityType = dbContext.Model.FindEntityType(typeof(IntegrationEventTransportMessage))
            ?? throw new InvalidOperationException("Integration event transport message entity is not configured.");
        var sql = CreateClaimSql(entityType);
        var command = FormattableStringFactory.Create(sql, consumerName, claimedBy, claimedUntil, now, batchSize);

        return await dbContext.Set<IntegrationEventTransportMessage>()
            .FromSql(command)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);
    }

    internal static string CreateClaimSql(IEntityType entityType)
    {
        var storeObject = StoreObjectIdentifier.Table(
            entityType.GetTableName() ?? throw new InvalidOperationException("Transport table name is not configured."),
            entityType.GetSchema());
        var tableName = FormatTableName(storeObject.Schema, storeObject.Name);
        var idColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.Id), storeObject));
        var consumerNameColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.ConsumerName), storeObject));
        var processedAtColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.ProcessedAt), storeObject));
        var nextAttemptColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.NextConsumeAttemptAt), storeObject));
        var claimedByColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.ClaimedBy), storeObject));
        var claimedUntilColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.ClaimedUntil), storeObject));
        var receivedAtColumn = FormatIdentifier(GetColumnName(entityType, nameof(IntegrationEventTransportMessage.ReceivedAt), storeObject));

        return $$"""
            WITH claimed AS (
                UPDATE {{tableName}} AS message
                SET {{claimedByColumn}} = {1}, {{claimedUntilColumn}} = {2}
                WHERE message.{{idColumn}} IN (
                    SELECT candidate.{{idColumn}}
                    FROM {{tableName}} AS candidate
                    WHERE candidate.{{consumerNameColumn}} = {0}
                        AND candidate.{{processedAtColumn}} IS NULL
                        AND (candidate.{{nextAttemptColumn}} IS NULL OR candidate.{{nextAttemptColumn}} <= {3})
                        AND (candidate.{{claimedUntilColumn}} IS NULL OR candidate.{{claimedUntilColumn}} <= {3})
                    ORDER BY candidate.{{receivedAtColumn}}
                    LIMIT {4}
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
        ?? throw new InvalidOperationException($"Transport column for '{propertyName}' is not configured.");

    private static string FormatTableName(string? schema, string table) => schema is null
        ? FormatIdentifier(table)
        : $"{FormatIdentifier(schema)}.{FormatIdentifier(table)}";

    private static string FormatIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
