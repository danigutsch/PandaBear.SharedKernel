namespace SharedKernel.Messaging.IntegrationEvents;

internal sealed class RegisteredIntegrationEventSerializer(
    IEnumerable<IIntegrationEventConsumerRegistration> registrations)
    : IIntegrationEventSerializer
{
    private readonly Dictionary<Type, IIntegrationEventConsumerRegistration> registrationsByType = registrations
        .ToDictionary(static registration => registration.IntegrationEventType);

    public string Serialize<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return registrationsByType.TryGetValue(typeof(TIntegrationEvent), out var registration)
            ? registration.Serialize(integrationEvent)
            : throw new NotSupportedException($"Integration event type '{typeof(TIntegrationEvent).FullName}' is not registered for durable serialization.");
    }
}
