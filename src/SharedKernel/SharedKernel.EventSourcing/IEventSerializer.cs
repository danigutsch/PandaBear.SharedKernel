namespace SharedKernel.EventSourcing;

/// <summary>
/// Serializes and deserializes event payloads for storage providers.
/// </summary>
public interface IEventSerializer
{
    /// <summary>
    /// Gets the stable event type name for a payload.
    /// </summary>
    /// <param name="eventData">The event payload.</param>
    /// <returns>The stable event type name.</returns>
    string GetEventType(object eventData);

    /// <summary>
    /// Serializes an event payload to JSON.
    /// </summary>
    /// <param name="eventData">The event payload.</param>
    /// <returns>The serialized event payload.</returns>
    string Serialize(object eventData);

    /// <summary>
    /// Deserializes an event payload from JSON.
    /// </summary>
    /// <param name="eventType">The stable event type name.</param>
    /// <param name="payloadJson">The serialized event payload.</param>
    /// <returns>The deserialized event payload.</returns>
    object Deserialize(string eventType, string payloadJson);
}
