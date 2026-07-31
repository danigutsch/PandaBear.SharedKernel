using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

internal sealed class IntegrationEventTransportPublisherCompatibilityAlias(
    IEventEnvelopePublisher publisher) : IEventEnvelopePublisher
{
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct) =>
        publisher.Publish(envelope, ct);

    internal static IEventEnvelopePublisher GetRequiredApplicationPublisher(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var resolvedPublisher = serviceProvider.GetRequiredService<IEventEnvelopePublisher>();
        if (resolvedPublisher is IntegrationEventTransportPublisherCompatibilityAlias)
        {
            throw new InvalidOperationException(
                $"Register an application {nameof(IEventEnvelopePublisher)} explicitly. The current unkeyed publisher is only " +
                "a PostgreSQL transport producer compatibility alias and cannot dispatch relay or consumer messages.");
        }

        return resolvedPublisher;
    }
}
