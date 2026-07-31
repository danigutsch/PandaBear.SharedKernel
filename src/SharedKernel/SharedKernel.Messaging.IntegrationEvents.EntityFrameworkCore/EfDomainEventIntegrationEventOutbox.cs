using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class EfDomainEventIntegrationEventOutbox(
    TimeProvider timeProvider,
    IIntegrationEventSerializer serializer)
    : IDomainEventIntegrationEventOutbox
{
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        var currentDbContext = CurrentSaveChangesDbContext.Current
            ?? throw new InvalidOperationException("No current SaveChanges context is available for the domain-event integration event outbox.");

        currentDbContext.Set<IntegrationEventOutboxMessage>().Add(IntegrationEventOutboxMessageFactory.Create(
            integrationEvent,
            serializer,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}
