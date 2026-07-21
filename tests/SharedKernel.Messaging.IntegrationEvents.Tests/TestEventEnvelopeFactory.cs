namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal static class TestEventEnvelopeFactory
{
    public static EventEnvelope Create(
        TestIntegrationEvent integrationEvent,
        string? payload,
        string? eventType = null) => new(
            "cloudevents",
            "1.0",
            integrationEvent.EventId.ToString(),
            new Uri("urn:test"),
            eventType ?? TestIntegrationEvent.EventType,
            TestIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            null,
            "application/json",
            null,
            payload,
            EventPayloadEncoding.Json,
            null);
}
