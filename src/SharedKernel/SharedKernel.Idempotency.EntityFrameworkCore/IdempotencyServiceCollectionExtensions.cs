using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SharedKernel.EntityFrameworkCore;

namespace SharedKernel.Idempotency.EntityFrameworkCore;

/// <summary>
/// Provides registration helpers for EF Core idempotency storage.
/// </summary>
public static class IdempotencyServiceCollectionExtensions
{
    /// <summary>
    /// Adds an EF Core idempotency store for the target DbContext.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the idempotency table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The store is keyed by <c>typeof(TContext)</c>; the first registration also remains available unkeyed for compatibility.</remarks>
    public static IServiceCollection AddIdempotencyStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return AddIdempotencyStoreCore<TContext>(services, configureStorage: null);
    }

    /// <summary>
    /// Adds an EF Core idempotency store with context-specific relational storage.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">The delegate that configures the context's idempotency table.</param>
    /// <typeparam name="TContext">The DbContext type that owns the idempotency table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>The store is keyed by <c>typeof(TContext)</c>; the first registration also remains available unkeyed for compatibility.</remarks>
    public static IServiceCollection AddIdempotencyStore<TContext>(
        this IServiceCollection services,
        Action<IdempotencyStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configureStorage);

        return AddIdempotencyStoreCore<TContext>(services, configureStorage);
    }

    private static IServiceCollection AddIdempotencyStoreCore<TContext>(
        IServiceCollection services,
        Action<IdempotencyStorageOptions>? configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<IdempotencyStorageOptions>(IdempotencyOptionsNames.Storage<TContext>()).ValidateOnStart();
        if (configureStorage is not null)
        {
            optionsBuilder.Configure(configureStorage);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<IdempotencyStorageOptions>,
                IdempotencyStorageOptionsValidator>());
        services.TryAddScoped<EfIdempotencyStore<TContext>>();
        services.TryAddKeyedScoped<IIdempotencyStore>(
            typeof(TContext),
            (serviceProvider, _) => serviceProvider.GetRequiredService<EfIdempotencyStore<TContext>>());
        services.TryAddScoped(serviceProvider =>
            serviceProvider.GetRequiredKeyedService<IIdempotencyStore>(typeof(TContext)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IdempotencyDbContextConfiguration<TContext>>());

        return services;
    }
}
