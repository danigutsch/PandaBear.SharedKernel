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
    /// <remarks>The outbox is keyed by <c>typeof(TContext)</c>; the first registration also remains available unkeyed for compatibility.</remarks>
    public static IServiceCollection AddIntegrationEventOutbox<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return AddIntegrationEventOutboxCore<TContext>(services, configureStorage: null);
    }

    /// <summary>
    /// Adds an EF Core integration event outbox with context-specific relational storage.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">The delegate that configures the context's integration-event tables.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The outbox is keyed by <c>typeof(TContext)</c>; the first registration also remains available unkeyed for compatibility.</remarks>
    public static IServiceCollection AddIntegrationEventOutbox<TContext>(
        this IServiceCollection services,
        Action<IntegrationEventStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configureStorage);

        return AddIntegrationEventOutboxCore<TContext>(services, configureStorage);
    }

    private static IServiceCollection AddIntegrationEventOutboxCore<TContext>(
        IServiceCollection services,
        Action<IntegrationEventStorageOptions>? configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        AddIntegrationEventStorageOptions<TContext>(services, configureStorage);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<EfIntegrationEventOutbox<TContext>>();
        services.TryAddKeyedScoped<IIntegrationEventOutbox>(
            typeof(TContext),
            (serviceProvider, _) => serviceProvider.GetRequiredService<EfIntegrationEventOutbox<TContext>>());
        services.TryAddScoped(serviceProvider =>
            serviceProvider.GetRequiredKeyedService<IIntegrationEventOutbox>(typeof(TContext)));
        services.TryAddSingleton<IDomainEventIntegrationEventOutbox, EfDomainEventIntegrationEventOutbox>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventOutboxDbContextConfiguration<TContext>>());

        return services;
    }

    /// <summary>
    /// Adds a DB-backed outbox relay for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">An optional delegate that configures relay behavior.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The relay resolves the envelope publisher keyed by <c>typeof(TContext)</c>.</remarks>
    public static IServiceCollection AddIntegrationEventOutboxRelay<TContext>(
        this IServiceCollection services,
        Action<IntegrationEventOutboxRelayOptions>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<IntegrationEventOutboxRelayOptions>(IntegrationEventOptionsNames.Relay<TContext>()).ValidateOnStart();
        if (configureOptions is not null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<IntegrationEventOutboxRelayOptions>,
                IntegrationEventOutboxRelayOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddKeyedScoped(
            typeof(TContext),
            (serviceProvider, _) =>
                IntegrationEventTransportPublisherCompatibilityAlias.GetRequiredApplicationPublisher(serviceProvider));
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
    /// <remarks>The store is keyed by <c>typeof(TContext)</c>; the first registration also remains available unkeyed for compatibility.</remarks>
    public static IServiceCollection AddIntegrationEventInbox<TContext>(this IServiceCollection services)
        where TContext : DbContext => services.AddIdempotencyStore<TContext>();

    /// <summary>
    /// Adds an EF Core integration event inbox with context-specific idempotency storage.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">The delegate that configures the context's idempotency table.</param>
    /// <typeparam name="TContext">The DbContext type that owns the inbox idempotency table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The store is keyed by <c>typeof(TContext)</c>; the first registration also remains available unkeyed for compatibility.</remarks>
    public static IServiceCollection AddIntegrationEventInbox<TContext>(
        this IServiceCollection services,
        Action<IdempotencyStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStorage);

        return services.AddIdempotencyStore<TContext>(configureStorage);
    }

    /// <summary>
    /// Adds a PostgreSQL-backed integration-event transport producer for one consumer queue.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="consumerName">The durable consumer queue name.</param>
    /// <typeparam name="TContext">The DbContext type that owns the transport table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The producer is keyed by <c>typeof(TContext)</c> and is registered unkeyed only when no publisher already exists.</remarks>
    public static IServiceCollection AddPostgreSqlIntegrationEventTransportProducer<TContext>(
        this IServiceCollection services,
        string consumerName)
        where TContext : DbContext
    {
        return AddPostgreSqlIntegrationEventTransportProducerCore<TContext>(services, consumerName, configureStorage: null);
    }

    /// <summary>
    /// Adds a PostgreSQL-backed integration-event transport producer with context-specific relational storage.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="consumerName">The durable consumer queue name.</param>
    /// <param name="configureStorage">The delegate that configures the context's integration-event tables.</param>
    /// <typeparam name="TContext">The DbContext type that owns the transport table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The producer is keyed by <c>typeof(TContext)</c> and is registered unkeyed only when no publisher already exists.</remarks>
    public static IServiceCollection AddPostgreSqlIntegrationEventTransportProducer<TContext>(
        this IServiceCollection services,
        string consumerName,
        Action<IntegrationEventStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configureStorage);

        return AddPostgreSqlIntegrationEventTransportProducerCore<TContext>(services, consumerName, configureStorage);
    }

    private static IServiceCollection AddPostgreSqlIntegrationEventTransportProducerCore<TContext>(
        IServiceCollection services,
        string consumerName,
        Action<IntegrationEventStorageOptions>? configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        AddIntegrationEventStorageOptions<TContext>(services, configureStorage);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventTransportDbContextConfiguration<TContext>>());
        services.AddKeyedScoped<IEventEnvelopePublisher>(
            typeof(TContext),
            (serviceProvider, _) => new PostgreSqlIntegrationEventTransportPublisher<TContext>(
                serviceProvider.GetRequiredService<TContext>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                consumerName));
        services.TryAddScoped<IEventEnvelopePublisher>(serviceProvider =>
            new IntegrationEventTransportPublisherCompatibilityAlias(
                serviceProvider.GetRequiredKeyedService<IEventEnvelopePublisher>(typeof(TContext))));

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

        AddIntegrationEventStorageOptions<TContext>(services, configureStorage: null);
        var optionsBuilder = services.AddOptions<IntegrationEventOutboxRelayOptions>(IntegrationEventOptionsNames.Consumer<TContext>()).ValidateOnStart();
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
            sp.GetRequiredService<IOptionsMonitor<IntegrationEventOutboxRelayOptions>>(),
            consumerName));
        services.AddHostedService<PostgreSqlIntegrationEventTransportConsumerHostedService<TContext>>();

        return services;
    }

    /// <summary>
    /// Configures context-specific integration-event storage before registering an outbox or transport component.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">The delegate that configures the context's integration-event tables.</param>
    /// <typeparam name="TContext">The DbContext type that owns or reads the configured tables.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection ConfigureIntegrationEventStorage<TContext>(
        this IServiceCollection services,
        Action<IntegrationEventStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStorage);

        AddIntegrationEventStorageOptions<TContext>(services, configureStorage);

        return services;
    }

    private static void AddIntegrationEventStorageOptions<TContext>(
        IServiceCollection services,
        Action<IntegrationEventStorageOptions>? configureStorage)
        where TContext : DbContext
    {
        var optionsBuilder = services.AddOptions<IntegrationEventStorageOptions>(IntegrationEventOptionsNames.Storage<TContext>()).ValidateOnStart();
        if (configureStorage is not null)
        {
            optionsBuilder.Configure(configureStorage);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<IntegrationEventStorageOptions>,
                IntegrationEventStorageOptionsValidator>());
    }
}
