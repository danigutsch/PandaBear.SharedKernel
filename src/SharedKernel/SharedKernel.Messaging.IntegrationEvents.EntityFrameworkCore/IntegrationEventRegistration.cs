using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PandaBear.Idempotency.EntityFrameworkCore;

namespace PandaBear.Messaging.IntegrationEvents.EntityFrameworkCore;

/// <summary>
/// Composes integration-event persistence and delivery for one context using the existing registrations.
/// </summary>
/// <typeparam name="TContext">The context that owns the selected messaging components.</typeparam>
public sealed class IntegrationEventRegistration<TContext>
    where TContext : DbContext
{
    private readonly IServiceCollection services;

    /// <summary>
    /// Creates a registration builder over the service collection.
    /// </summary>
    /// <param name="services">Collection that receives the registrations.</param>
    internal IntegrationEventRegistration(IServiceCollection services)
    {
        this.services = services;
    }

    /// <summary>
    /// Registers the context's outbox with optional relational storage configuration.
    /// </summary>
    /// <param name="configureStorage">Optional context-specific storage configuration.</param>
    /// <returns>This registration builder.</returns>
    public IntegrationEventRegistration<TContext> WithOutbox(Action<IntegrationEventStorageOptions>? configureStorage = null)
    {
        if (configureStorage is null)
        {
            services.AddIntegrationEventOutbox<TContext>();
        }
        else
        {
            services.AddIntegrationEventOutbox<TContext>(configureStorage);
        }

        return this;
    }

    /// <summary>
    /// Registers the context's inbox idempotency store.
    /// </summary>
    /// <param name="configureStorage">Optional context-specific idempotency storage configuration.</param>
    /// <returns>This registration builder.</returns>
    public IntegrationEventRegistration<TContext> WithInbox(Action<IdempotencyStorageOptions>? configureStorage = null)
    {
        if (configureStorage is null)
        {
            services.AddIntegrationEventInbox<TContext>();
        }
        else
        {
            services.AddIntegrationEventInbox<TContext>(configureStorage);
        }

        return this;
    }

    /// <summary>
    /// Makes this context's outbox the one resolved without a context key when several contexts share a host.
    /// </summary>
    /// <returns>This registration builder.</returns>
    public IntegrationEventRegistration<TContext> AsDefaultOutbox()
    {
        services.UseDefaultIntegrationEventOutbox<TContext>();

        return this;
    }

    /// <summary>
    /// Makes this context's inbox idempotency store the one resolved without a context key when several contexts share
    /// a host.
    /// </summary>
    /// <returns>This registration builder.</returns>
    public IntegrationEventRegistration<TContext> AsDefaultInbox()
    {
        services.UseDefaultIdempotencyStore<TContext>();

        return this;
    }

    /// <summary>
    /// Registers and hosts the context's outbox relay.
    /// </summary>
    /// <param name="configureOptions">Optional relay configuration.</param>
    /// <returns>This registration builder.</returns>
    public IntegrationEventRegistration<TContext> WithOutboxRelay(Action<IntegrationEventOutboxRelayOptions>? configureOptions = null)
    {
        services.AddIntegrationEventOutboxRelay<TContext>(configureOptions);

        return this;
    }

    /// <summary>
    /// Selects PostgreSQL atomic outbox claims instead of the provider-neutral default.
    /// </summary>
    /// <returns>This registration builder.</returns>
    public IntegrationEventRegistration<TContext> WithPostgreSqlAtomicClaims()
    {
        services.AddPostgreSqlIntegrationEventOutboxRelayAtomicClaims<TContext>();

        return this;
    }

    /// <summary>
    /// Registers a context-keyed PostgreSQL transport producer without replacing the application dispatcher.
    /// </summary>
    /// <param name="consumerName">The durable destination queue name.</param>
    /// <returns>This registration builder.</returns>
    /// <exception cref="ArgumentException">The queue name is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The context already has an envelope publisher.</exception>
    public IntegrationEventRegistration<TContext> WithPostgreSqlTransportProducer(string consumerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        if (services.Any(descriptor => descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(IEventEnvelopePublisher)
            && Equals(descriptor.ServiceKey, typeof(TContext))))
        {
            throw new InvalidOperationException("An envelope publisher is already registered for this context.");
        }

        services.AddPostgreSqlIntegrationEventTransportProducer<TContext>(consumerName);

        return this;
    }

    /// <summary>
    /// Registers a PostgreSQL queue consumer without starting background delivery.
    /// </summary>
    /// <param name="consumerName">The durable queue name.</param>
    /// <param name="configureOptions">Optional consumer polling configuration.</param>
    /// <returns>This registration builder.</returns>
    /// <exception cref="ArgumentException">The queue name is null, empty, or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A consumer is already registered for this context.</exception>
    public IntegrationEventRegistration<TContext> WithPostgreSqlTransportConsumer(
        string consumerName,
        Action<IntegrationEventOutboxRelayOptions>? configureOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        if (HasConsumer())
        {
            throw new InvalidOperationException("A transport consumer is already registered for this context.");
        }

        IntegrationEventOutboxServiceCollectionExtensions.AddPostgreSqlIntegrationEventTransportConsumerCore<TContext>(
            services, consumerName, configureOptions);

        return this;
    }

    /// <summary>
    /// Enables background delivery in the host that owns this context's registered consumer.
    /// </summary>
    /// <returns>This registration builder.</returns>
    /// <exception cref="InvalidOperationException">No transport consumer is registered for this context.</exception>
    public IntegrationEventRegistration<TContext> WithHostedConsumer()
    {
        if (!HasConsumer())
        {
            throw new InvalidOperationException("Register a transport consumer before enabling hosted delivery.");
        }

        services.AddHostedService<PostgreSqlIntegrationEventTransportConsumerHostedService<TContext>>();

        return this;
    }

    private bool HasConsumer() => services.Any(descriptor => !descriptor.IsKeyedService
        && descriptor.ServiceType == typeof(PostgreSqlIntegrationEventTransportConsumer<TContext>));
}
