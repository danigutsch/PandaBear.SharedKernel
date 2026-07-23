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
