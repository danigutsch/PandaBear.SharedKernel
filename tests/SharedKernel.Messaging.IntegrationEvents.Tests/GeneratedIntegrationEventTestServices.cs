using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Messaging.IntegrationEvents.Tests;

/// <summary>
/// Factory methods that set up service collections for the generated integration event tests.
/// </summary>
internal static class GeneratedIntegrationEventTestServices
{
    /// <summary>
    /// Returns an action that registers the TestIntegrationEvent contract under the given event type when invoked.
    /// </summary>
    public static Action CreateContractRegistration(string eventType)
    {
        var services = new ServiceCollection();
        JsonTypeInfo<TestIntegrationEvent> jsonTypeInfo = TestIntegrationEventJsonContext.Default.TestIntegrationEvent;
        return () => services.AddIntegrationEventContract(eventType, jsonTypeInfo);
    }

    /// <summary>
    /// Registers both test event contracts and the generated integration events, then returns the resolved serializer.
    /// </summary>
    public static IIntegrationEventSerializer CreateSerializer()
    {
        var services = new ServiceCollection();
        services.AddTestIntegrationEventContracts();
        services.AddGeneratedIntegrationEvents();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IIntegrationEventSerializer>();
    }

    /// <summary>
    /// Creates the consumer host that wires scoped handlers and generated dispatch.
    /// </summary>
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
