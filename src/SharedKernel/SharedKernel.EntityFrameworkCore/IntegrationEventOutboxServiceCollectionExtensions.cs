using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.IntegrationEvents;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Provides registration helpers for the EF Core integration event outbox.
/// </summary>
public static class IntegrationEventOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Adds an EF Core integration event outbox for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the outbox table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventOutbox<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IIntegrationEventOutbox, EfIntegrationEventOutbox<TContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IntegrationEventOutboxDbContextConfiguration<TContext>>());
        services.AddIdempotencyStore<TContext>();

        return services;
    }
}
