using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PandaBear.EntityFrameworkCore;

namespace PandaBear.DispositionHold.EntityFrameworkCore;

/// <summary>Provides registration helpers for EF Core disposition hold storage.</summary>
public static class DispositionHoldServiceCollectionExtensions
{
    /// <summary>Adds the hold store and guard for the target DbContext.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <typeparam name="TContext">The DbContext type that owns the hold table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <remarks>
    /// The store and guard resolve unkeyed and are bound to <typeparamref name="TContext" />. They join whatever
    /// transaction the caller has open on that context, and refuse to answer without one.
    /// </remarks>
    public static IServiceCollection AddDispositionHolds<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return AddDispositionHoldsCore<TContext>(services, configureStorage: null);
    }

    /// <summary>Adds the hold store and guard for the target DbContext with context-specific relational storage.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureStorage">The delegate that configures the context's hold table.</param>
    /// <typeparam name="TContext">The DbContext type that owns the hold table.</typeparam>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configureStorage"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddDispositionHolds<TContext>(
        this IServiceCollection services,
        Action<DispositionHoldStorageOptions> configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configureStorage);

        return AddDispositionHoldsCore<TContext>(services, configureStorage);
    }

    private static IServiceCollection AddDispositionHoldsCore<TContext>(
        IServiceCollection services,
        Action<DispositionHoldStorageOptions>? configureStorage)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<DispositionHoldStorageOptions>(DispositionHoldOptionsNames.Storage<TContext>()).ValidateOnStart();
        if (configureStorage is not null)
        {
            optionsBuilder.Configure(configureStorage);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<DispositionHoldStorageOptions>, DispositionHoldStorageOptionsValidator>());
        services.TryAddScoped<EfDispositionHoldStore<TContext>>();
        services.TryAddScoped<IDispositionHoldStore>(serviceProvider => serviceProvider.GetRequiredService<EfDispositionHoldStore<TContext>>());
        services.TryAddScoped<IDispositionHoldGuard>(serviceProvider => serviceProvider.GetRequiredService<EfDispositionHoldStore<TContext>>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDbContextConfiguration<TContext>, DispositionHoldDbContextConfiguration<TContext>>());

        return services;
    }
}
