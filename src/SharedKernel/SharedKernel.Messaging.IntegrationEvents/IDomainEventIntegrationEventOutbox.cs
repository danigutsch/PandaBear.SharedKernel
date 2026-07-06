namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Stores integration events created while domain events are dispatched during an EF Core save operation.
/// </summary>
public interface IDomainEventIntegrationEventOutbox
{
    /// <summary>
    /// Enqueues the integration event in the current domain-event dispatch unit of work.
    /// </summary>
    /// <param name="integrationEvent">The integration event to enqueue.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <returns>A task that completes when the event has been enqueued.</returns>
    ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent;
}
