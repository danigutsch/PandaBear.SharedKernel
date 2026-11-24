using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Builds the atomic, parameterized PostgreSQL write for a retained completed duplicate observation.</summary>
internal static class IntegrationEventInboxDuplicateSql
{
    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>Uses the configured inbox and observation mappings to build the command.</summary>
    /// <exception cref="InvalidOperationException">The PostgreSQL inbox or observation table is unavailable.</exception>
    internal static string Create(DbContext context)
    {
        if (!string.Equals(context.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Inbox duplicate recording requires EF Core provider '{NpgsqlProviderName}'.");
        }

        var observation = context.Model.FindEntityType(typeof(IntegrationEventInboxDuplicateObservation))
            ?? throw new InvalidOperationException("Inbox duplicate observations are not configured for this context.");
        var idempotency = context.Model.FindEntityType(typeof(IdempotencyEntryEntity))
            ?? throw new InvalidOperationException("The inbox idempotency entry is not configured for this context.");
        var sql = context.GetService<ISqlGenerationHelper>();
        var source = Table(idempotency);
        var destination = Table(observation);
        var inboxTable = EscapeFormat(sql.DelimitIdentifier(source.Name, source.Schema));
        var observationTable = EscapeFormat(sql.DelimitIdentifier(destination.Name, destination.Schema));
        var sourceScope = Column(idempotency, nameof(IdempotencyEntryEntity.Scope), source, sql);
        var sourceKey = Column(idempotency, nameof(IdempotencyEntryEntity.Key), source, sql);
        var sourceState = Column(idempotency, nameof(IdempotencyEntryEntity.State), source, sql);
        var scope = Column(observation, nameof(IntegrationEventInboxDuplicateObservation.Scope), destination, sql);
        var key = Column(observation, nameof(IntegrationEventInboxDuplicateObservation.Key), destination, sql);
        var consumer = Column(observation, nameof(IntegrationEventInboxDuplicateObservation.ConsumerName), destination, sql);
        var eventId = Column(observation, nameof(IntegrationEventInboxDuplicateObservation.EventId), destination, sql);
        var count = Column(observation, nameof(IntegrationEventInboxDuplicateObservation.DuplicateCount), destination, sql);
        var time = Column(observation, nameof(IntegrationEventInboxDuplicateObservation.LastObservedAt), destination, sql);

        // Take the parent lock before writing the dependent row, in the same order as retention.
        return $$"""
            INSERT INTO {{observationTable}} AS observation ({{scope}}, {{key}}, {{consumer}}, {{eventId}}, {{count}}, {{time}})
            SELECT entry.{{sourceScope}}, entry.{{sourceKey}}, {2}, {3}, 1, {4}
            FROM {{inboxTable}} AS entry
            WHERE entry.{{sourceScope}} = {0} AND entry.{{sourceKey}} = {1} AND entry.{{sourceState}} = {5}
            FOR KEY SHARE OF entry
            ON CONFLICT ({{consumer}}, {{eventId}}) DO UPDATE
            SET {{count}} = CASE WHEN observation.{{count}} < {6} THEN observation.{{count}} + 1 ELSE observation.{{count}} END,
                {{time}} = GREATEST(observation.{{time}}, EXCLUDED.{{time}})
            WHERE observation.{{scope}} = EXCLUDED.{{scope}} AND observation.{{key}} = EXCLUDED.{{key}}
            """;
    }

    private static StoreObjectIdentifier Table(IEntityType entity) =>
        StoreObjectIdentifier.Create(entity, StoreObjectType.Table)
        ?? throw new InvalidOperationException("The inbox entity must be mapped to a PostgreSQL table.");

    private static string Column(IEntityType entity, string property, StoreObjectIdentifier table, ISqlGenerationHelper sql) =>
        EscapeFormat(sql.DelimitIdentifier(entity.FindProperty(property)?.GetColumnName(table)
            ?? throw new InvalidOperationException($"Inbox property '{property}' has no mapped column.")));

    private static string EscapeFormat(string identifier) =>
        identifier.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
}
