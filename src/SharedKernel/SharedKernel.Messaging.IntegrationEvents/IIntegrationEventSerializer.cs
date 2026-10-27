namespace PandaBear.Messaging.IntegrationEvents;

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

    /// <summary>
    /// Serializes an integration event together with the encoding and content type of its payload.
    /// The default implementation returns the <see cref="Serialize{TIntegrationEvent}"/> text as JSON; a serializer for another format overrides it.
    /// </summary>
    /// <param name="integrationEvent">The integration event to serialize.</param>
    /// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
    /// <returns>The serialized payload with its encoding and content type.</returns>
    SerializedIntegrationEventPayload SerializePayload<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent =>
        new(Serialize(integrationEvent), EventPayloadEncoding.Json, "application/json");
}
