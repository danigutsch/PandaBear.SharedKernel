using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PandaBear.Observability.Npgsql.Tests;

/// <summary>Holds a service collection populated by AddPostgreSqlIndexHealthMonitoring so tests can inspect the resulting registrations.</summary>
internal sealed class PostgreSqlIndexHealthMonitoringRegistrationScope
{
    private readonly ServiceCollection _services = [];

    private PostgreSqlIndexHealthMonitoringRegistrationScope()
    {
    }

    /// <summary>Gets the number of hosted service descriptors in the collection.</summary>
    public int HostedServiceCount => _services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService));

    /// <summary>Gets the number of connection strings held by the single monitoring registration.</summary>
    public int RegisteredConnectionCount => GetRegistration().ConnectionStrings.Count;

    /// <summary>Gets the implementation type of the only registered hosted service; throws if there is not exactly one.</summary>
    public Type? HostedServiceImplementationType => _services
        .Single(descriptor => descriptor.ServiceType == typeof(IHostedService))
        .ImplementationType;

    /// <summary>Creates a scope registering monitoring for the connection strings with default options.</summary>
    public static PostgreSqlIndexHealthMonitoringRegistrationScope Create(IEnumerable<string> connectionStrings)
    {
        return Create(connectionStrings, new PostgreSqlIndexHealthMonitoringOptions());
    }

    /// <summary>Creates a scope registering monitoring for the connection strings with the supplied options.</summary>
    public static PostgreSqlIndexHealthMonitoringRegistrationScope Create(
        IEnumerable<string> connectionStrings,
        PostgreSqlIndexHealthMonitoringOptions options)
    {
        var scope = new PostgreSqlIndexHealthMonitoringRegistrationScope();
        scope.Add(connectionStrings, options);
        return scope;
    }

    /// <summary>Calls AddPostgreSqlIndexHealthMonitoring on the scope's service collection, allowing repeated registration.</summary>
    public void Add(IEnumerable<string> connectionStrings, PostgreSqlIndexHealthMonitoringOptions options)
    {
        _services.AddPostgreSqlIndexHealthMonitoring(connectionStrings, options);
    }

    private PostgreSqlIndexHealthMonitoringRegistration GetRegistration()
    {
        var registration = _services
            .Single(descriptor => descriptor.ServiceType == typeof(PostgreSqlIndexHealthMonitoringRegistration))
            .ImplementationInstance as PostgreSqlIndexHealthMonitoringRegistration;

        return registration ?? throw new InvalidOperationException("PostgreSQL index-health monitoring registration is missing.");
    }
}
