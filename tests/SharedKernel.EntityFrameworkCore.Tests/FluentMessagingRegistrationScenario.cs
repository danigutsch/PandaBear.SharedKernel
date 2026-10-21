using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

internal static class FluentMessagingRegistrationScenario
{
    public static int CountHostedConsumers(bool hosted)
    {
        var services = new ServiceCollection();
        var registration = services.AddIntegrationEvents<FirstMessagingDbContext>()
            .WithPostgreSqlTransportConsumer("first-consumer");
        if (hosted)
        {
            registration.WithHostedConsumer().WithHostedConsumer();
        }

        return services.Count(descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(PostgreSqlIntegrationEventTransportConsumerHostedService<FirstMessagingDbContext>));
    }

    public static int CountLegacyHostedConsumers()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlIntegrationEventTransportConsumer<FirstMessagingDbContext>("first-consumer");

        return services.Count(descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(PostgreSqlIntegrationEventTransportConsumerHostedService<FirstMessagingDbContext>));
    }

    public static void HostAnotherContextsConsumer()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportConsumer("first-consumer");
        services.AddIntegrationEvents<SecondMessagingDbContext>().WithHostedConsumer();
    }

    public static void HostWithoutConsumer()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithHostedConsumer();
    }

    public static void RegisterCompetingConsumers()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportConsumer("first");
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportConsumer("second");
    }

    public static void RegisterCompetingProducers()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportProducer("first");
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportProducer("second");
    }
}
