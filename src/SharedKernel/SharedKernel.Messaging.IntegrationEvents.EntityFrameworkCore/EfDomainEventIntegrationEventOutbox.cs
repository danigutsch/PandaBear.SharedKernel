using Microsoft.EntityFrameworkCore;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class EfDomainEventIntegrationEventOutbox<TContext>(
    TimeProvider timeProvider,
    IIntegrationEventSerializer serializer)
    : IDomainEventIntegrationEventOutbox
    where TContext : DbContext
{
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        if (CurrentSaveChangesDbContext.Current is not TContext currentDbContext)
        {
            throw new InvalidOperationException($"No current {typeof(TContext).Name} SaveChanges context is available for the domain-event integration event outbox.");
        }

        currentDbContext.Set<IntegrationEventOutboxMessage>().Add(IntegrationEventOutboxMessageFactory.Create(
            integrationEvent,
            serializer,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}
