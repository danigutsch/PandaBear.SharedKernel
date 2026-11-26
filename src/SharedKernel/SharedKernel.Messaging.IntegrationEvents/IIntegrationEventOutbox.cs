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

    /// <summary>
    /// Enqueues the integration event with an application-supplied workflow correlation identifier.
    /// </summary>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <param name="integrationEvent">The integration event to enqueue.</param>
    /// <param name="workflowCorrelationId">The nonempty identifier shared by events in one workflow.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the enqueue operation.</returns>
    /// <exception cref="ArgumentException">A supporting provider rejects an empty workflow correlation identifier.</exception>
    /// <exception cref="NotSupportedException">The configured outbox does not support workflow correlation.</exception>
    ValueTask Enqueue<TIntegrationEvent>(
        TIntegrationEvent integrationEvent, Guid workflowCorrelationId, CancellationToken ct)
        where TIntegrationEvent : IIntegrationEvent =>
        throw new NotSupportedException("This integration event outbox does not support workflow correlation.");
}
