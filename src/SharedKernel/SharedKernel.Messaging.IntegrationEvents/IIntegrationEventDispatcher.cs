namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Dispatches typed integration events to matching handlers.
/// </summary>
public interface IIntegrationEventDispatcher
{
    /// <summary>
    /// Dispatches the specified integration event.
    /// </summary>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <param name="integrationEvent">The integration event instance to dispatch.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when dispatch finishes.</returns>
    ValueTask Dispatch<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent;
}
