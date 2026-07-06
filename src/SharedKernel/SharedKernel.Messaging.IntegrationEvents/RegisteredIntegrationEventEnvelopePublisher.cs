namespace SharedKernel.Messaging.IntegrationEvents;

internal sealed class RegisteredIntegrationEventEnvelopePublisher(
    IServiceProvider serviceProvider,
    IEnumerable<IIntegrationEventConsumerRegistration> registrations)
    : IEventEnvelopePublisher
{
    private readonly Dictionary<string, IIntegrationEventConsumerRegistration> registrationsByEventType = registrations
        .ToDictionary(static registration => registration.EventType, StringComparer.Ordinal);

    public async ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!registrationsByEventType.TryGetValue(envelope.EventType, out var registration))
        {
            throw new NotSupportedException($"Integration event type '{envelope.EventType}' is not registered for delivery.");
        }

        await registration.Publish(serviceProvider, envelope, ct).ConfigureAwait(false);
    }
}
