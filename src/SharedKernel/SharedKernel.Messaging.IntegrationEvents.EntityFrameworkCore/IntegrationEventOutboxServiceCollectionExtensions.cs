using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// <param name="lifetime">The outbox service lifetime.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventOutbox<TContext>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddIntegrationEventOutbox<TContext>(lifetime);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventOutboxDbContextConfiguration<TContext>>());
        services.AddIdempotencyStore<TContext>();

        return services;
    }

    private static void TryAddIntegrationEventOutbox<TContext>(this IServiceCollection services, ServiceLifetime lifetime)
        where TContext : DbContext
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<IIntegrationEventOutbox>(sp => new EfIntegrationEventOutbox<TContext>(
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<IIntegrationEventSerializer>()));
                return;
            case ServiceLifetime.Scoped:
                services.TryAddScoped<IIntegrationEventOutbox, EfIntegrationEventOutbox<TContext>>();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Only singleton and scoped outbox lifetimes are supported.");
        }
    }

    /// <summary>
    /// Adds a DB-backed outbox relay for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventOutboxRelay<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<EfIntegrationEventOutboxRelay<TContext>>();
        services.AddHostedService<IntegrationEventOutboxRelayHostedService<TContext>>();

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
}
