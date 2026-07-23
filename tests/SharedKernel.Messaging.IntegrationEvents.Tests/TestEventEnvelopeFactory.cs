namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal static class TestEventEnvelopeFactory
{
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
