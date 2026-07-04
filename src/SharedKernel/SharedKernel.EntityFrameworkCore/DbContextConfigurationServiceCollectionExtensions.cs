using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.EntityFrameworkCore;

/// <summary>
/// Provides registration helpers for composable DbContext configuration.
/// </summary>
public static class DbContextConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Adds a DbContext configuration instance.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration to add.</param>
    /// <typeparam name="TContext">The DbContext type being configured.</typeparam>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddDbContextConfiguration<TContext>(
        this IServiceCollection services,
        IDbContextConfiguration<TContext> configuration)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(configuration);

        return services;
    }

    /// <summary>
    /// Applies registered DbContext options configurations in registration order.
    /// </summary>
    /// <param name="services">The service collection containing configuration instances.</param>
    /// <param name="options">The EF Core options builder.</param>
    /// <typeparam name="TContext">The DbContext type being configured.</typeparam>
    public static void ApplyDbContextOptionConfigurations<TContext>(
        this IServiceCollection services,
        DbContextOptionsBuilder options)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IDbContextConfiguration<TContext>)
                && descriptor.ImplementationInstance is IDbContextConfiguration<TContext> configuration)
            {
                configuration.ConfigureOptions(options);
            }
        }
    }
}
