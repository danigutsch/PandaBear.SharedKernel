using Microsoft.Extensions.DependencyInjection;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Outbox for integration events raised from domain-event handlers: it adds the message to the DbContext that is currently saving, so the event commits atomically with the aggregate change.
/// </summary>
/// <param name="timeProvider">Clock that stamps the enqueue time.</param>
/// <param name="serviceProvider">Provider used to resolve the serializer for the current context.</param>
internal sealed class EfDomainEventIntegrationEventOutbox(
    TimeProvider timeProvider,
    IServiceProvider serviceProvider)
    : IDomainEventIntegrationEventOutbox
{
    /// <inheritdoc />
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
