using Microsoft.Extensions.DependencyInjection;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class EfDomainEventIntegrationEventOutbox(
    TimeProvider timeProvider,
    IServiceProvider serviceProvider)
    : IDomainEventIntegrationEventOutbox
{
    public ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ct.ThrowIfCancellationRequested();

        var currentDbContext = CurrentSaveChangesDbContext.Current
            ?? throw new InvalidOperationException("No current SaveChanges context is available for the domain-event integration event outbox.");
        var serializer = serviceProvider.GetKeyedService<IIntegrationEventSerializer>(currentDbContext.GetType())
            ?? serviceProvider.GetRequiredService<IIntegrationEventSerializer>();

        currentDbContext.Set<IntegrationEventOutboxMessage>().Add(IntegrationEventOutboxMessageFactory.Create(
            integrationEvent,
            serializer,
            timeProvider.GetUtcNow()));

        return ValueTask.CompletedTask;
    }
}
