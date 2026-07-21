using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal static class GeneratedIntegrationEventTestServices
{
    public static Action CreateContractRegistration(string eventType)
    {
        var services = new ServiceCollection();
        return () => services.AddIntegrationEventContract(
            eventType,
            TestIntegrationEventJsonContext.Default.TestIntegrationEvent);
    }

    public static IIntegrationEventSerializer CreateSerializer()
    {
        var services = new ServiceCollection();
        services.AddTestIntegrationEventContract();
        services.AddGeneratedIntegrationEvents();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IIntegrationEventSerializer>();
    }

    public static GeneratedIntegrationEventConsumerHost CreateConsumerHost()
    {
        return GeneratedIntegrationEventConsumerHost.Create();
    }

    private static void AddTestIntegrationEventContract(this IServiceCollection services)
    {
        services.AddIntegrationEventContract(
            TestIntegrationEvent.EventType,
            TestIntegrationEventJsonContext.Default.TestIntegrationEvent);
    }

}
