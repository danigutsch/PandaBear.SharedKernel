using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PandaBear.Messaging.IntegrationEvents;

namespace PandaBear.Messaging.Protobuf;

/// <summary>
/// Registers consumers for Protobuf integration events.
/// </summary>
public static class ProtobufIntegrationEventConsumerServiceCollectionExtensions
{
    private static readonly object InnerPublisherKey = new();

    /// <summary>
    /// Registers a handler for a Protobuf integration event and delivers envelopes of that event type to it.
    /// </summary>
    /// <remarks>
    /// The first call replaces the unkeyed <see cref="IEventEnvelopePublisher"/> with one that handles Protobuf envelopes
    /// (<see cref="EventPayloadEncoding.Base64"/> with <see cref="ProtobufContentTypes.Payload"/>) and passes every other envelope to the
    /// publisher registered before it, which keeps its lifetime and is resolved as a keyed service. Call it after
    /// <c>AddGeneratedIntegrationEvents</c> so that the generated JSON consumers stay reachable; called earlier, a JSON envelope fails with a
    /// <see cref="NotSupportedException"/> that names this requirement instead of being dropped.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TIntegrationEvent">The Protobuf integration event type.</typeparam>
    /// <typeparam name="THandler">The handler type, registered as a scoped <see cref="IIntegrationEventHandler{TIntegrationEvent}"/> when none is registered.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a Protobuf consumer is already registered for the event type.</exception>
    public static IServiceCollection AddProtobufIntegrationEventConsumer<TIntegrationEvent, THandler>(this IServiceCollection services)
        where TIntegrationEvent : IIntegrationEvent, IMessage<TIntegrationEvent>, new()
        where THandler : class, IIntegrationEventHandler<TIntegrationEvent>
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ProtobufConsumerRegistration)
            && descriptor.ImplementationInstance is ProtobufConsumerRegistration existing
            && string.Equals(existing.EventType, TIntegrationEvent.EventType, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"A Protobuf consumer is already registered for integration event type '{TIntegrationEvent.EventType}'.");
        }

        InstallPublisher(services);
        services.TryAddScoped<IIntegrationEventHandler<TIntegrationEvent>, THandler>();
        services.AddSingleton(new ProtobufConsumerRegistration(
            TIntegrationEvent.EventType,
            static async (scopeFactory, envelope, ct) =>
            {
                var payload = envelope.Payload
                    ?? throw new InvalidOperationException($"Integration event '{envelope.EventType}' payload is required.");
                var integrationEvent = ProtobufIntegrationEventSerializer.Deserialize<TIntegrationEvent>(payload);
                var scope = scopeFactory.CreateAsyncScope();
                await using var _ = scope.ConfigureAwait(false);
                var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<TIntegrationEvent>>();
                await handler.Handle(integrationEvent, ct).ConfigureAwait(false);
            }));
        return services;
    }

    private static void InstallPublisher(IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ProtobufPublisherInstalled)))
        {
            return;
        }

        var existing = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IEventEnvelopePublisher) && !descriptor.IsKeyedService);
        var hasInner = existing is not null;
        if (existing is not null)
        {
            services.Remove(existing);
            services.Add(ToKeyedInner(existing));
        }

        // The registration of this type only marks that the publisher is installed; the factory below is the real registration.
        services.AddSingleton(new ProtobufPublisherInstalled());
        services.AddScoped<IEventEnvelopePublisher>(provider => new ProtobufEventEnvelopePublisher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetServices<ProtobufConsumerRegistration>(),
            hasInner ? provider.GetRequiredKeyedService<IEventEnvelopePublisher>(InnerPublisherKey) : null));
    }

    private static ServiceDescriptor ToKeyedInner(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType is { } implementationType)
        {
            return new ServiceDescriptor(typeof(IEventEnvelopePublisher), InnerPublisherKey, implementationType, descriptor.Lifetime);
        }

        if (descriptor.ImplementationFactory is { } factory)
        {
            return new ServiceDescriptor(typeof(IEventEnvelopePublisher), InnerPublisherKey, (provider, _) => factory(provider), descriptor.Lifetime);
        }

        return new ServiceDescriptor(typeof(IEventEnvelopePublisher), InnerPublisherKey, descriptor.ImplementationInstance!);
    }
}
