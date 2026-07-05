using Microsoft.EntityFrameworkCore;
using SharedKernel.IntegrationEvents;

namespace SharedKernel.EntityFrameworkCore;

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

        dbContext.Set<IntegrationEventOutboxMessage>().Add(new IntegrationEventOutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = TIntegrationEvent.EventType,
            EventVersion = TIntegrationEvent.EventVersion,
            EventId = integrationEvent.EventId,
            OccurredAt = integrationEvent.OccurredAt,
            PayloadJson = serializer.Serialize(integrationEvent),
            EnqueuedAt = timeProvider.GetUtcNow(),
        });

        return ValueTask.CompletedTask;
    }
}
