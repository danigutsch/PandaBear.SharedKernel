using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Stores integration events in the current EF Core unit of work.
/// </summary>
/// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
internal sealed class EfIntegrationEventOutbox<TContext>(
    TContext dbContext,
    TimeProvider timeProvider,
    IIntegrationEventSerializer serializer) : IIntegrationEventOutbox
    where TContext : DbContext
{
    /// <inheritdoc />
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        var envelope = new EventEnvelope(
            integrationEvent.EventId,
            TIntegrationEvent.EventType,
            TIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            serializer.Serialize(integrationEvent));

        dbContext.Set<IntegrationEventOutboxMessageEntity>().Add(new IntegrationEventOutboxMessageEntity(
            Guid.CreateVersion7(),
            envelope,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}
