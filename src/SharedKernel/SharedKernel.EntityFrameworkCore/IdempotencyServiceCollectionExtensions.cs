using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Idempotency;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Provides registration helpers for EF Core integration-event inbox idempotency storage.
/// </summary>
public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Adds an EF Core integration-event inbox for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the inbox idempotency table.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddIntegrationEventInbox<TContext>(this IServiceCollection services)
        where TContext : DbContext => services.AddIdempotencyStore<TContext>();

    internal static IServiceCollection AddIdempotencyStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IIdempotencyStore, EfIdempotencyStore<TContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IdempotencyDbContextConfiguration<TContext>>());

        return services;
    }
}
