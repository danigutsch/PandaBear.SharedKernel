using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal static class GeneratedIntegrationEventTestServices
{
    public static Action CreateContractRegistration(string eventType)
    {
        var services = new ServiceCollection();
        JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo = TestIntegrationEventJsonContext.Default.TestIntegrationEvent;
        return () => services.AddIntegrationEventContract(eventType, jsonTypeInfo);
    }

    public static IIntegrationEventSerializer CreateSerializer()
    {
        var services = new ServiceCollection();
        services.AddTestIntegrationEventContracts();
        services.AddGeneratedIntegrationEvents();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IIntegrationEventSerializer>();
    }

    public static GeneratedIntegrationEventConsumerHost CreateConsumerHost()
    {
        return GeneratedIntegrationEventConsumerHost.Create();
    }

    private static void AddTestIntegrationEventContracts(this IServiceCollection services)
    {
        JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo = TestIntegrationEventJsonContext.Default.TestIntegrationEvent;
        services.AddIntegrationEventContract(TestIntegrationEvent.EventType, jsonTypeInfo);
        JsonTypeInfo<TestUpdatedIntegrationEvent> updatedJsonTypeInfo =
            TestIntegrationEventJsonContext.Default.TestUpdatedIntegrationEvent;
        services.AddIntegrationEventContract(TestUpdatedIntegrationEvent.EventType, updatedJsonTypeInfo);
    }

}
