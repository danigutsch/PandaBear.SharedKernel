namespace SharedKernel.Messaging.IntegrationEvents;

internal interface IIntegrationEventConsumerRegistration
{
    string EventType { get; }

    Type IntegrationEventType { get; }

    string Serialize(IIntegrationEvent integrationEvent);

    IIntegrationEvent Deserialize(string? payload, string eventType);

    ValueTask Publish(IServiceProvider serviceProvider, EventEnvelope envelope, CancellationToken ct);
}
