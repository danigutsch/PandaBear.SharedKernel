using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace SharedKernel.Observability.Npgsql;

/// <summary>Registers reusable, read-only PostgreSQL index-health monitoring.</summary>
public static class PostgreSqlIndexHealthServiceCollectionExtensions
{
    /// <summary>Registers one hosted monitor for the supplied dedicated monitoring connections.</summary>
    /// <param name="services">The service collection receiving the monitor.</param>
    /// <param name="connectionStrings">Dedicated least-privilege PostgreSQL monitoring connections.</param>
    /// <param name="options">The bounded polling and command-timeout options.</param>
    /// <returns>The original service collection.</returns>
    /// <exception cref="ArgumentException">Thrown when the supplied connections are missing or invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when monitoring has already been registered.</exception>
    public static IServiceCollection AddPostgreSqlIndexHealthMonitoring(
        this IServiceCollection services,
        IEnumerable<string> connectionStrings,
        PostgreSqlIndexHealthMonitoringOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionStrings);
        ArgumentNullException.ThrowIfNull(options);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(PostgreSqlIndexHealthMonitoringRegistration)))
        {
            throw new InvalidOperationException("PostgreSQL index-health monitoring is already registered.");
        }

        options.Validate();
        var monitoringConnectionStrings = connectionStrings.ToArray();
        if (monitoringConnectionStrings.Length == 0 || monitoringConnectionStrings.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("PostgreSQL index-health monitoring requires dedicated connection strings.", nameof(connectionStrings));
        }

        foreach (var connectionString in monitoringConnectionStrings)
        {
            try
            {
                _ = new NpgsqlConnectionStringBuilder(connectionString);
            }
            catch (ArgumentException)
            {
                throw new ArgumentException("PostgreSQL index-health monitoring requires valid connection strings.", nameof(connectionStrings));
            }
        }

        services.AddSingleton(new PostgreSqlIndexHealthMonitoringRegistration(monitoringConnectionStrings, options));
        services.AddHostedService<PostgreSqlIndexHealthHostedService>();

        return services;
    }
}
