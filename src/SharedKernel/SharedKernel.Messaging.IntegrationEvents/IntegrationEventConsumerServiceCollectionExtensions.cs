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
        if (!string.Equals(eventType, TIntegrationEvent.EventType, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Integration event contract '{typeof(TIntegrationEvent).FullName}' declares event type '{TIntegrationEvent.EventType}', not '{eventType}'.",
                nameof(eventType));
        }

        services.TryAddSingleton(jsonTypeInfo);

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

        return services;
    }
}
