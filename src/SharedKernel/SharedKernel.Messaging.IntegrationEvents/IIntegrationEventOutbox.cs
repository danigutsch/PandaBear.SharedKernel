namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Stores integration events durably for later publication.
/// </summary>
public interface IIntegrationEventOutbox
{
    /// <summary>
    /// Enqueues the integration event in the current unit of work.
    /// </summary>
    /// <param name="integrationEvent">The integration event to enqueue.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the enqueue operation.</returns>
    ValueTask Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent;
}
