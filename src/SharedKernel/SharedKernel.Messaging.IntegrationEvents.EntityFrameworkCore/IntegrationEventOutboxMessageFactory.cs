using System.Diagnostics;
using System.Text.Json;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Builds outbox rows from integration events.</summary>
internal static class IntegrationEventOutboxMessageFactory
{
    private const string JsonContentType = "application/json";
    private const string Source = "urn:sharedkernel:integration-events";
    private const string TraceParentExtensionAttribute = "traceparent";
    private const string TraceStateExtensionAttribute = "tracestate";

    /// <summary>Serializes the event into a CloudEvents-style outbox message and carries the current trace context so the relay can continue the trace.</summary>
    public static IntegrationEventOutboxMessage Create<TIntegrationEvent>(
        TIntegrationEvent integrationEvent,
        IIntegrationEventSerializer serializer,
        DateTimeOffset enqueuedAt)
        where TIntegrationEvent : IIntegrationEvent => new(
            Guid.CreateVersion7(),
            IntegrationEventEnvelopeConstants.Spec,
            IntegrationEventEnvelopeConstants.SpecVersion,
            integrationEvent.EventId.ToString("D"),
            new Uri(Source),
            TIntegrationEvent.EventType,
            TIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            null,
            JsonContentType,
            null,
            serializer.Serialize(integrationEvent),
            EventPayloadEncoding.Json,
            CreateTraceExtensionAttributesJson(),
            enqueuedAt);

    private static string? CreateTraceExtensionAttributesJson()
    {
        var currentActivity = Activity.Current;
        if (currentActivity?.Id is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(currentActivity.TraceStateString))
        {
            return $$"""{"{{TraceParentExtensionAttribute}}":"{{JsonEncodedText.Encode(currentActivity.Id)}}","{{TraceStateExtensionAttribute}}":"{{JsonEncodedText.Encode(currentActivity.TraceStateString)}}"}""";
        }

        return $$"""{"{{TraceParentExtensionAttribute}}":"{{JsonEncodedText.Encode(currentActivity.Id)}}"}""";
    }
}
