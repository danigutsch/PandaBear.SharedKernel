using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents;

internal sealed class IntegrationEventConsumerRegistration<TIntegrationEvent>(
    string eventType,
    JsonTypeInfo<TIntegrationEvent> jsonTypeInfo)
    : IIntegrationEventConsumerRegistration
    where TIntegrationEvent : IIntegrationEvent
{
    public string EventType { get; } = eventType;

    public Type IntegrationEventType => typeof(TIntegrationEvent);

    public string Serialize(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return integrationEvent is TIntegrationEvent typedEvent
            ? JsonSerializer.Serialize(typedEvent, jsonTypeInfo)
            : throw new NotSupportedException($"Integration event type '{integrationEvent.GetType().FullName}' is not registered for durable serialization.");
    }

    public async ValueTask Publish(IServiceProvider serviceProvider, EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(envelope);

        var integrationEvent = DeserializeTyped(envelope.Payload, envelope.EventType);
        var handler = serviceProvider.GetRequiredService<IIntegrationEventHandler<TIntegrationEvent>>();
        await handler.Handle(integrationEvent, ct).ConfigureAwait(false);
    }

    public IIntegrationEvent Deserialize(string? payload, string eventType) => DeserializeTyped(payload, eventType);

    private TIntegrationEvent DeserializeTyped(string? payload, string envelopeEventType)
    {
        if (payload is null)
        {
            throw new InvalidOperationException($"Integration event '{envelopeEventType}' payload is required.");
        }

        return JsonSerializer.Deserialize(payload, jsonTypeInfo)
            ?? throw new InvalidOperationException($"Integration event '{envelopeEventType}' payload could not be deserialized.");
    }
}
