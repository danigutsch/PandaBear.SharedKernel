namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Handles a typed integration event.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type handled by the handler.</typeparam>
public interface IIntegrationEventHandler<in TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    /// <summary>
    /// Handles the integration event.
    /// </summary>
    /// <param name="integrationEvent">The integration event to handle.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A task that completes when handling finishes.</returns>
    ValueTask Handle(TIntegrationEvent integrationEvent, CancellationToken ct);
}
