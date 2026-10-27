using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PandaBear.Messaging;
using PandaBear.Messaging.IntegrationEvents;
using PandaBear.Messaging.IntegrationEvents.CloudEvents;
using PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Builders and helpers for the consumer outbox, relay and transport tests.
/// </summary>
internal static class ConsumerMessagingTestServices
{
    /// <summary>
    /// Creates an outbox scope with a recording publisher and the system clock.
    /// </summary>
    public static ConsumerMessagingTestScope CreateOutboxScope()
    {
        return new ConsumerMessagingTestScope();
    }

    /// <summary>
    /// Builds a validated provider with the integration event outbox and relay on a uniquely named in-memory ConsumerOutboxDbContext, using the given publisher and time provider.
    /// </summary>
    public static ServiceProvider CreateOutboxRelayProvider(
        IEventEnvelopePublisher publisher,
        TimeProvider timeProvider,
        Action<IntegrationEventOutboxRelayOptions>? configureOptions = null,
        string? databaseName = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(publisher);
        services.AddSingleton(timeProvider);
        services.AddSingleton<IIntegrationEventSerializer>(new ContextQualifiedMessagingTestIntegrationEventSerializer("{}"));
        services.AddIntegrationEventOutbox<ConsumerOutboxDbContext>(options => options.Schema = "messaging");
        services.AddIntegrationEventOutboxRelay<ConsumerOutboxDbContext>(configureOptions);
        var resolvedDatabaseName = databaseName ?? Guid.NewGuid().ToString("N");
        services.AddDbContext<ConsumerOutboxDbContext>(options =>
        {
            options.UseInMemoryDatabase(resolvedDatabaseName);
            services.ApplyDbContextOptionConfigurations<ConsumerOutboxDbContext>(options, enableDevelopmentDiagnostics: false);
        });
        configureServices?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    /// <summary>
    /// Enqueues a ConsumerTestIntegrationEvent with the given code through the outbox and saves it.
    /// </summary>
    public static async ValueTask EnqueueConsumerTestIntegrationEvent(ServiceProvider provider, string slug)
    {
        ArgumentNullException.ThrowIfNull(provider);

        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsumerOutboxDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<IIntegrationEventOutbox>();
        await outbox.Enqueue(new ConsumerTestIntegrationEvent(
            Guid.CreateVersion7(),
            new DateTimeOffset(2026, 6, 22, 11, 59, 0, TimeSpan.Zero),
            Guid.CreateVersion7(),
            slug,
            "Andes Relay 2026"), TestContext.Current.CancellationToken);
        _ = await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Returns the only outbox message stored in the provider's context and fails when there is not exactly one.
    /// </summary>
    public static IntegrationEventOutboxMessage GetSingleOutboxMessage(ServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ConsumerOutboxDbContext>()
            .Set<IntegrationEventOutboxMessage>()
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// Marks the only outbox message as claimed by "test-relay" until the given time and saves it.
    /// </summary>
    public static void ClaimSingleOutboxMessage(ServiceProvider provider, DateTimeOffset claimedUntil)
    {
        ArgumentNullException.ThrowIfNull(provider);

        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsumerOutboxDbContext>();
        var message = dbContext.Set<IntegrationEventOutboxMessage>().ShouldHaveSingleItem();
        message.MarkClaimed("test-relay", claimedUntil);
        _ = dbContext.SaveChanges();
    }

    /// <summary>
    /// Returns the single registered claim-strategy descriptor after adding the PostgreSQL atomic claims to the relay.
    /// </summary>
    public static ServiceDescriptor GetPostgreSqlOutboxRelayClaimStrategyDescriptor()
    {
        var services = new ServiceCollection();
        services.AddIntegrationEventOutboxRelay<ConsumerOutboxDbContext>();
        services.AddPostgreSqlIntegrationEventOutboxRelayAtomicClaims<ConsumerOutboxDbContext>();

        return services
            .Where(service => service.ServiceType == typeof(IIntegrationEventOutboxClaimStrategy<ConsumerOutboxDbContext>))
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// Returns the single IHostedService descriptor registered by the PostgreSQL transport consumer.
    /// </summary>
    public static ServiceDescriptor GetPostgreSqlTransportConsumerHostedServiceDescriptor()
    {
        var services = new ServiceCollection();
        services.AddPostgreSqlIntegrationEventTransportConsumer<ConsumerOutboxDbContext>("consumer");

        return services
            .Where(service => service.ServiceType == typeof(IHostedService))
            .ShouldHaveSingleItem();
    }

    /// <summary>
    /// Creates a validated provider with the PostgreSQL transport producer on an in-memory context and wraps it in a scenario.
    /// </summary>
    public static ConsumerTransportScenario CreateTransportScenario(TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddPostgreSqlIntegrationEventTransportProducer<ConsumerTransportDbContext>("consumer");
        services.AddDbContext<ConsumerTransportDbContext>(options =>
        {
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
            services.ApplyDbContextOptionConfigurations<ConsumerTransportDbContext>(options, enableDevelopmentDiagnostics: false);
        });

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        try
        {
            return new ConsumerTransportScenario(provider);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates a JSON CloudEvents envelope for ConsumerTestIntegrationEvent with the given event id.
    /// </summary>
    public static EventEnvelope CreateEnvelope(string eventId) => new(
        CloudEventConstants.Spec,
        CloudEventConstants.SpecVersion,
        eventId,
        new Uri("urn:test:source"),
        ConsumerTestIntegrationEvent.EventType,
        ConsumerTestIntegrationEvent.EventVersion,
        new DateTimeOffset(2026, 6, 22, 11, 59, 0, TimeSpan.Zero),
        "tour-1",
        "application/json",
        null,
        "{}",
        EventPayloadEncoding.Json,
        null);

}
