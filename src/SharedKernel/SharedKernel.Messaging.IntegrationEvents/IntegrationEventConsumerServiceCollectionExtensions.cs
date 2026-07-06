using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SharedKernel.Messaging.IntegrationEvents;

/// <summary>
/// Registers explicit integration event consumers and payload metadata.
/// </summary>
public static class IntegrationEventConsumerServiceCollectionExtensions
{
    /// <summary>
    /// Registers one explicit integration event contract for durable serialization.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="eventType">The stable event type identifier.</param>
    /// <param name="jsonTypeInfo">The AOT-safe JSON metadata for the event contract.</param>
    /// <typeparam name="TIntegrationEvent">The integration event contract type.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventContract<TIntegrationEvent>(
        this IServiceCollection services,
        string eventType,
        JsonTypeInfo<TIntegrationEvent> jsonTypeInfo)
        where TIntegrationEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(jsonTypeInfo);

        services.TryAddSingleton<RegisteredIntegrationEventSerializer>();
        services.TryAddSingleton<IIntegrationEventSerializer>(sp => sp.GetRequiredService<RegisteredIntegrationEventSerializer>());
        var existingRegistration = services
            .Where(static descriptor => descriptor.ServiceType == typeof(IIntegrationEventConsumerRegistration))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<IIntegrationEventConsumerRegistration>()
            .FirstOrDefault(registration => registration.IntegrationEventType == typeof(TIntegrationEvent) || registration.EventType == eventType);
        if (existingRegistration is not null)
        {
            if (existingRegistration.IntegrationEventType == typeof(TIntegrationEvent) && existingRegistration.EventType == eventType)
            {
                return services;
            }

            throw new InvalidOperationException(
                $"Integration event registration conflict for event type '{eventType}' and contract type '{typeof(TIntegrationEvent).FullName}'.");
        }

        services.AddSingleton<IIntegrationEventConsumerRegistration>(
            new IntegrationEventConsumerRegistration<TIntegrationEvent>(eventType, jsonTypeInfo));

        return services;
    }

    /// <summary>
    /// Registers one explicit integration event contract for envelope delivery.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="eventType">The stable event type identifier.</param>
    /// <param name="jsonTypeInfo">The AOT-safe JSON metadata for the event contract.</param>
    /// <typeparam name="TIntegrationEvent">The integration event contract type.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventConsumer<TIntegrationEvent>(
        this IServiceCollection services,
        string eventType,
        JsonTypeInfo<TIntegrationEvent> jsonTypeInfo)
        where TIntegrationEvent : IIntegrationEvent
    {
        services.AddIntegrationEventContract(eventType, jsonTypeInfo);
        services.TryAddScoped<IEventEnvelopePublisher, RegisteredIntegrationEventEnvelopePublisher>();

        return services;
    }
}
