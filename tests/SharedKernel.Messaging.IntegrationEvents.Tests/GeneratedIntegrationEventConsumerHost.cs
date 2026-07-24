using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

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

    public IReadOnlyList<CapturingIntegrationEventHandler> Handlers => handlers;

    public IIntegrationEventSerializer Serializer => provider.GetRequiredService<IIntegrationEventSerializer>();

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

    public GeneratedIntegrationEventConsumerScope OpenDelivery()
    {
        return new GeneratedIntegrationEventConsumerScope(provider);
    }

    public void Dispose()
    {
        provider.Dispose();
    }
}
