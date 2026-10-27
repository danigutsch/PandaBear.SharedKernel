using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.Protobuf;

/// <summary>
/// Delivers Protobuf envelopes to their registered handlers and passes every other envelope to the publisher that was registered before it.
/// </summary>
/// <param name="scopeFactory">Creates the scope in which a handler runs.</param>
/// <param name="registrations">The registered Protobuf consumers.</param>
/// <param name="inner">The publisher for non-Protobuf envelopes, such as the generated JSON publisher, or null when none is registered.</param>
internal sealed class ProtobufEventEnvelopePublisher(
    IServiceScopeFactory scopeFactory,
    IEnumerable<ProtobufConsumerRegistration> registrations,
    IEventEnvelopePublisher? inner) : IEventEnvelopePublisher
{
    /// <inheritdoc />
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        var isProtobuf = envelope.PayloadEncoding == EventPayloadEncoding.Base64
            && string.Equals(envelope.DataContentType, ProtobufContentTypes.Payload, StringComparison.OrdinalIgnoreCase);
        if (isProtobuf)
        {
            var registration = registrations.FirstOrDefault(candidate => string.Equals(candidate.EventType, envelope.EventType, StringComparison.Ordinal))
                ?? throw new NotSupportedException(
                    $"No Protobuf consumer is registered for integration event type '{envelope.EventType}'. Register it with AddProtobufIntegrationEventConsumer.");
            return registration.Handle(scopeFactory, envelope, ct);
        }

        if (inner is not null)
        {
            return inner.Publish(envelope, ct);
        }

        throw new NotSupportedException(
            $"Integration event envelope type '{envelope.EventType}' is not registered for delivery: only Protobuf consumers are registered. "
            + "To deliver JSON events too, call AddProtobufIntegrationEventConsumer after AddGeneratedIntegrationEvents or after registering the IEventEnvelopePublisher.");
    }
}
