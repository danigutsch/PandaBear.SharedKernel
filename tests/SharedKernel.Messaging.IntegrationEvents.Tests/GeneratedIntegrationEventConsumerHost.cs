using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal sealed class GeneratedIntegrationEventConsumerHost : IDisposable
{
    private readonly ServiceProvider provider;

    private GeneratedIntegrationEventConsumerHost(ServiceProvider provider)
    {
        this.provider = provider;
    }

    public IIntegrationEventSerializer Serializer => provider.GetRequiredService<IIntegrationEventSerializer>();

    public static GeneratedIntegrationEventConsumerHost Create()
    {
        var services = new ServiceCollection();
        services.AddScoped<CapturingIntegrationEventHandler>();
        services.AddScoped<IIntegrationEventHandler<TestIntegrationEvent>>(
            static serviceProvider => serviceProvider.GetRequiredService<CapturingIntegrationEventHandler>());
        JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo = TestIntegrationEventJsonContext.Default.TestIntegrationEvent;
        services.AddIntegrationEventConsumer(
            TestIntegrationEvent.EventType,
            jsonTypeInfo);
        services.AddGeneratedIntegrationEvents();

        return new GeneratedIntegrationEventConsumerHost(services.BuildServiceProvider());
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
