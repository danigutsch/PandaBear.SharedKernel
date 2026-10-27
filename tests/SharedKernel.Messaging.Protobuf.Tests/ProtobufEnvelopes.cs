namespace PandaBear.Messaging.Protobuf.Tests;

/// <summary>Creates envelopes the way the outbox records them.</summary>
internal static class ProtobufEnvelopes
{
    /// <summary>Creates the envelope for an event serialized with <see cref="ProtobufIntegrationEventSerializer"/>.</summary>
    /// <param name="integrationEvent">The event.</param>
    /// <returns>An envelope with a Base64 payload and the <c>application/protobuf</c> content type.</returns>
    internal static EventEnvelope ForProtobuf(TourCreated integrationEvent)
    {
        var payload = new ProtobufIntegrationEventSerializer().SerializePayload(integrationEvent);
        return Create(TourCreated.EventType, payload.Payload, payload.Encoding, payload.DataContentType);
    }

    /// <summary>Creates an envelope with the given type, payload, encoding, and content type.</summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="payload">The payload text.</param>
    /// <param name="encoding">The payload encoding.</param>
    /// <param name="dataContentType">The payload content type.</param>
    /// <returns>The envelope.</returns>
    internal static EventEnvelope Create(string eventType, string payload, EventPayloadEncoding encoding, string dataContentType) =>
        new("cloudevents", "1.0", Guid.NewGuid().ToString("D"), new Uri("urn:test"), eventType, 1, null, null, dataContentType, null, payload, encoding, null);
}
