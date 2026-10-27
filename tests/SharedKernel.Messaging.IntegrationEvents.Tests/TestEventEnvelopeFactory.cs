namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// Creates event envelopes for integration event tests.
/// </summary>
internal static class TestEventEnvelopeFactory
{
    /// <summary>
    /// Creates a CloudEvents 1.0 envelope with JSON encoding from the event's id, time and static type and version, optionally overriding the event type and carrying the given payload.
    /// </summary>
    public static EventEnvelope Create<TIntegrationEvent>(
        TIntegrationEvent integrationEvent,
        string? payload,
        string? eventType = null)
        where TIntegrationEvent : IIntegrationEvent => new(
            "cloudevents",
            "1.0",
            integrationEvent.EventId.ToString(),
            new Uri("urn:test"),
            eventType ?? TIntegrationEvent.EventType,
            TIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            null,
            "application/json",
            null,
            payload,
            EventPayloadEncoding.Json,
            null);
}
