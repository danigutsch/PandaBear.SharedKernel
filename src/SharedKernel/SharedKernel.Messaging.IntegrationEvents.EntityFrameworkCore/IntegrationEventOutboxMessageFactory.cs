using System.Diagnostics;
using System.Text.Json;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>Builds outbox rows from integration events.</summary>
internal static class IntegrationEventOutboxMessageFactory
{
    private const string Source = "urn:sharedkernel:integration-events";
    private const string TraceParentExtensionAttribute = "traceparent";
    private const string TraceStateExtensionAttribute = "tracestate";

    /// <summary>Serializes the event into an outbox message with optional workflow and trace metadata.</summary>
    public static IntegrationEventOutboxMessage Create<TIntegrationEvent>(
        TIntegrationEvent integrationEvent,
        IIntegrationEventSerializer serializer,
        DateTimeOffset enqueuedAt,
        Guid? workflowCorrelationId = null)
        where TIntegrationEvent : IIntegrationEvent
    {
        var payload = serializer.SerializePayload(integrationEvent);
        return new(
            Guid.CreateVersion7(),
            IntegrationEventEnvelopeConstants.Spec,
            IntegrationEventEnvelopeConstants.SpecVersion,
            integrationEvent.EventId.ToString("D"),
            new Uri(Source),
            TIntegrationEvent.EventType,
            TIntegrationEvent.EventVersion,
            integrationEvent.OccurredAt,
            null,
            payload.DataContentType,
            null,
            payload.Payload,
            payload.Encoding,
            CreateExtensionAttributesJson(workflowCorrelationId),
            enqueuedAt,
            workflowCorrelationId);
    }

    private static string? CreateExtensionAttributesJson(Guid? workflowCorrelationId)
    {
        var traceAttributes = CreateTraceExtensionAttributesJson();
        if (workflowCorrelationId is not { } id)
        {
            return traceAttributes;
        }

        var workflowAttribute = $"\"{WorkflowCorrelationExtension.AttributeName}\":\"{id:D}\"";
        return traceAttributes is null ? $"{{{workflowAttribute}}}" : $"{traceAttributes[..^1]},{workflowAttribute}}}";
    }

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
