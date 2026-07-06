using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Messaging.IntegrationEvents.Tests;

internal static class RegisteredIntegrationEventTestServices
{
    public static IIntegrationEventSerializer CreateSerializer()
    {
        var services = new ServiceCollection();
        services.AddTestIntegrationEventContract();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IIntegrationEventSerializer>();
    }

    public static ServiceProvider CreateConsumerProvider(CapturingIntegrationEventHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>>(handler);
        services.AddTestIntegrationEventConsumer();

        return services.BuildServiceProvider();
    }

    public static ServiceProvider CreateConsumerProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<TestIntegrationEvent>, TestIntegrationEventHandler>();
        services.AddTestIntegrationEventConsumer();

        return services.BuildServiceProvider();
    }

    private static void AddTestIntegrationEventContract(this IServiceCollection services)
    {
        services.AddIntegrationEventContract(
            TestIntegrationEvent.EventType,
            TestIntegrationEventJsonContext.Default.TestIntegrationEvent);
    }

    private static void AddTestIntegrationEventConsumer(this IServiceCollection services)
    {
        services.AddIntegrationEventConsumer(
            TestIntegrationEvent.EventType,
            TestIntegrationEventJsonContext.Default.TestIntegrationEvent);
    }
}
