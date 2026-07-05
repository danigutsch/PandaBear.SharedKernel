namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Serializes integration events for durable storage.
/// </summary>
public interface IIntegrationEventSerializer
{
    /// <summary>
    /// Serializes an integration event.
    /// </summary>
    /// <param name="integrationEvent">The integration event to serialize.</param>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <returns>The serialized payload.</returns>
    string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent;
}
