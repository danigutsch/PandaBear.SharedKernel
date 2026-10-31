using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.EntityFrameworkCore;

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
    /// <exception cref="ArgumentNullException">When <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
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
    /// Applies registered DbContext options configurations in registration order, after the development diagnostics when requested.
    /// </summary>
    /// <param name="services">The service collection containing configuration instances.</param>
    /// <param name="options">The EF Core options builder.</param>
    /// <param name="enableDevelopmentDiagnostics">
    /// Whether to enable detailed errors and sensitive data logging. Pass <see langword="true"/> only in local development:
    /// sensitive data logging writes parameter values to the log.
    /// </param>
    /// <typeparam name="TContext">The DbContext type being configured.</typeparam>
    /// <exception cref="ArgumentNullException">When <paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static void ApplyDbContextOptionConfigurations<TContext>(
        this IServiceCollection services,
        DbContextOptionsBuilder options,
        bool enableDevelopmentDiagnostics)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        if (enableDevelopmentDiagnostics)
        {
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
        }

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
