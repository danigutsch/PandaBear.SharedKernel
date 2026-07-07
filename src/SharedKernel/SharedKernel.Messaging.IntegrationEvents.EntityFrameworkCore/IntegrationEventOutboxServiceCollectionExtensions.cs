using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SharedKernel.EntityFrameworkCore;
using SharedKernel.Idempotency.EntityFrameworkCore;

namespace SharedKernel.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Provides registration helpers for the EF Core integration event outbox.
/// </summary>
public static class IntegrationEventOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Adds an EF Core integration event outbox whose migrations own the shared messaging table.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventOutbox<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IIntegrationEventOutbox, EfIntegrationEventOutbox<TContext>>();
        services.TryAddSingleton<IDomainEventIntegrationEventOutbox, EfDomainEventIntegrationEventOutbox<TContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventOutboxDbContextConfiguration<TContext>>());
        services.AddIdempotencyStore<TContext>();

        return services;
    }

    /// <summary>
    /// Adds a DB-backed outbox relay for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">An optional delegate that configures relay behavior.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventOutboxRelay<TContext>(
        this IServiceCollection services,
        Action<IntegrationEventOutboxRelayOptions>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<IntegrationEventOutboxRelayOptions>().ValidateOnStart();
        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<IntegrationEventOutboxRelayOptions>,
                IntegrationEventOutboxRelayOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIntegrationEventOutboxClaimStrategy<TContext>, EfIntegrationEventOutboxClaimStrategy<TContext>>();
        services.AddSingleton<EfIntegrationEventOutboxRelay<TContext>>();
        services.AddHostedService<IntegrationEventOutboxRelayHostedService<TContext>>();

        return services;
    }

    /// <summary>
    /// Uses PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> atomic claims for the outbox relay.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddPostgreSqlIntegrationEventOutboxRelayAtomicClaims<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(
            ServiceDescriptor.Singleton<
                IIntegrationEventOutboxClaimStrategy<TContext>,
                PostgreSqlIntegrationEventOutboxClaimStrategy<TContext>>());

        return services;
    }

    /// <summary>
    /// Adds an EF Core integration event inbox for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the inbox idempotency table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventInbox<TContext>(this IServiceCollection services)
        where TContext : DbContext => services.AddIdempotencyStore<TContext>();

    /// <summary>
    /// Adds a PostgreSQL-backed integration-event transport producer for one consumer queue.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="consumerName">The durable consumer queue name.</param>
    /// <typeparam name="TContext">The DbContext type that owns the transport table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddPostgreSqlIntegrationEventTransportProducer<TContext>(
        this IServiceCollection services,
        string consumerName)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventTransportDbContextConfiguration<TContext>>());
        services.Replace(ServiceDescriptor.Scoped<IEventEnvelopePublisher>(sp => new PostgreSqlIntegrationEventTransportPublisher<TContext>(
            sp.GetRequiredService<TContext>(),
            sp.GetRequiredService<TimeProvider>(),
            consumerName)));

        return services;
    }

    /// <summary>
    /// Adds a PostgreSQL-backed integration-event transport consumer for one durable queue.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="consumerName">The durable consumer queue name.</param>
    /// <param name="configureOptions">An optional delegate that configures polling behavior.</param>
    /// <typeparam name="TContext">The DbContext type that reads the transport table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddPostgreSqlIntegrationEventTransportConsumer<TContext>(
        this IServiceCollection services,
        string consumerName,
        Action<IntegrationEventOutboxRelayOptions>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        var optionsBuilder = services.AddOptions<IntegrationEventOutboxRelayOptions>().ValidateOnStart();
        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<IntegrationEventOutboxRelayOptions>,
                IntegrationEventOutboxRelayOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventTransportDbContextConfiguration<TContext>>());
        services.AddSingleton(sp => new PostgreSqlIntegrationEventTransportConsumer<TContext>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<IntegrationEventOutboxRelayOptions>>(),
            consumerName));
        services.AddHostedService<PostgreSqlIntegrationEventTransportConsumerHostedService<TContext>>();

        return services;
    }
}
