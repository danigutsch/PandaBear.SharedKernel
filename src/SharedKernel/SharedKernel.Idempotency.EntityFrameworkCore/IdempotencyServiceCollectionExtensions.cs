using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.Idempotency.EntityFrameworkCore;

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
    /// <remarks>The store is keyed by <c>typeof(TContext)</c>. It is also the unkeyed store when it is the only context registered or the one chosen with <see cref="UseDefaultIdempotencyStore{TContext}" />.</remarks>
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
    /// <exception cref="ArgumentNullException">When <paramref name="configureStorage"/> is <see langword="null"/>.</exception>
    /// <remarks>The store is keyed by <c>typeof(TContext)</c>. It is also the unkeyed store when it is the only context registered or the one chosen with <see cref="UseDefaultIdempotencyStore{TContext}" />.</remarks>
    public static IServiceCollection AddIdempotencyStore<TContext>(
        this IServiceCollection services,
        Action<IdempotencyStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configureStorage);

        return AddIdempotencyStoreCore<TContext>(services, configureStorage);
    }

    /// <summary>
    /// Chooses the context whose idempotency store is resolved without a context key when several contexts register
    /// one. Without a choice, resolving the unkeyed store fails rather than binding whichever registered first.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type whose store becomes the unkeyed default.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection UseDefaultIdempotencyStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        ContextKeyedDefault<IIdempotencyStore>.Choose(services, typeof(TContext));
        ContextKeyedDefault<IIdempotencyRetentionStore>.Choose(services, typeof(TContext));
        return services;
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
        services.TryAddScoped<EfIdempotencyRetentionStore<TContext>>();
        services.TryAddKeyedScoped<IIdempotencyRetentionStore>(
            typeof(TContext),
            (serviceProvider, _) => serviceProvider.GetRequiredService<EfIdempotencyRetentionStore<TContext>>());
        ContextKeyedDefault<IIdempotencyRetentionStore>.Register(services, typeof(TContext), nameof(UseDefaultIdempotencyStore));
        services.TryAddKeyedScoped<IIdempotencyStore>(
            typeof(TContext),
            (serviceProvider, _) => serviceProvider.GetRequiredService<EfIdempotencyStore<TContext>>());
        ContextKeyedDefault<IIdempotencyStore>.Register(services, typeof(TContext), nameof(UseDefaultIdempotencyStore));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, IdempotencyDbContextConfiguration<TContext>>());

        return services;
    }
}
