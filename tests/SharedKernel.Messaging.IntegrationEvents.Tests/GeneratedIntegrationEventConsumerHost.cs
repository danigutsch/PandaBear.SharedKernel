using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// A service provider with scoped capturing handlers, JSON consumers for both test events and the generated integration event registrations, used to test generated dispatch.
/// </summary>
internal sealed class GeneratedIntegrationEventConsumerHost : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly IReadOnlyList<CapturingIntegrationEventHandler> handlers;

    private GeneratedIntegrationEventConsumerHost(
        ServiceProvider provider,
        IReadOnlyList<CapturingIntegrationEventHandler> handlers)
    {
        this.provider = provider;
        this.handlers = handlers;
    }

    /// <summary>
    /// Gets every handler instance created by the provider, one per resolved scope and event type.
    /// </summary>
    public IReadOnlyList<CapturingIntegrationEventHandler> Handlers => handlers;

    /// <summary>
    /// Gets the integration event serializer resolved from the provider.
    /// </summary>
    public IIntegrationEventSerializer Serializer => provider.GetRequiredService<IIntegrationEventSerializer>();

    /// <summary>
    /// Builds a host with scope validation enabled, handlers for both test events, consumers for both event types and the generated registrations.
    /// </summary>
    public static GeneratedIntegrationEventConsumerHost Create()
    {
        var services = new ServiceCollection();
        var handlers = new List<CapturingIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<TestIntegrationEvent>>(_ =>
        {
            var handler = new CapturingIntegrationEventHandler();
            handlers.Add(handler);
            return handler;
        });
        services.AddScoped<IIntegrationEventHandler<TestUpdatedIntegrationEvent>>(_ =>
        {
            var handler = new CapturingIntegrationEventHandler();
            handlers.Add(handler);
            return handler;
        });
        JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo = TestIntegrationEventJsonContext.Default.TestIntegrationEvent;
        services.AddIntegrationEventConsumer(
            TestIntegrationEvent.EventType,
            jsonTypeInfo);
        JsonTypeInfo<TestUpdatedIntegrationEvent> updatedJsonTypeInfo =
            TestIntegrationEventJsonContext.Default.TestUpdatedIntegrationEvent;
        services.AddIntegrationEventConsumer(
            TestUpdatedIntegrationEvent.EventType,
            updatedJsonTypeInfo);
        services.AddGeneratedIntegrationEvents();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        return new GeneratedIntegrationEventConsumerHost(provider, handlers);
    }

    /// <summary>
    /// Opens a new delivery scope that resolves the event envelope publisher.
    /// </summary>
    public GeneratedIntegrationEventConsumerScope OpenDelivery()
    {
        return new GeneratedIntegrationEventConsumerScope(provider);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        provider.Dispose();
    }
}
