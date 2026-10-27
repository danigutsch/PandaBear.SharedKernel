using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Registration scenarios for the fluent integration-event builder, used by FluentMessagingRegistrationTests.
/// </summary>
internal static class FluentMessagingRegistrationScenario
{
    /// <summary>
    /// Registers a PostgreSQL transport consumer, optionally calls WithHostedConsumer twice, and counts the resulting hosted-service registrations for the first context.
    /// </summary>
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

    /// <summary>
    /// Registers a consumer with the older AddPostgreSqlIntegrationEventTransportConsumer method and counts the hosted-service registrations for the first context.
    /// </summary>
    public static int CountLegacyHostedConsumers()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlIntegrationEventTransportConsumer<FirstMessagingDbContext>("first-consumer");

        return services.Count(descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(PostgreSqlIntegrationEventTransportConsumerHostedService<FirstMessagingDbContext>));
    }

    /// <summary>
    /// Calls WithHostedConsumer for the second context while only the first context has a transport consumer.
    /// </summary>
    public static void HostAnotherContextsConsumer()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportConsumer("first-consumer");
        services.AddIntegrationEvents<SecondMessagingDbContext>().WithHostedConsumer();
    }

    /// <summary>
    /// Calls WithHostedConsumer on a context that has no transport consumer registered.
    /// </summary>
    public static void HostWithoutConsumer()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithHostedConsumer();
    }

    /// <summary>
    /// Registers two PostgreSQL transport consumers named "first" and "second" for the same context.
    /// </summary>
    public static void RegisterCompetingConsumers()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportConsumer("first");
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportConsumer("second");
    }

    /// <summary>
    /// Registers two PostgreSQL transport producers named "first" and "second" for the same context.
    /// </summary>
    public static void RegisterCompetingProducers()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportProducer("first");
        services.AddIntegrationEvents<FirstMessagingDbContext>().WithPostgreSqlTransportProducer("second");
    }
}
