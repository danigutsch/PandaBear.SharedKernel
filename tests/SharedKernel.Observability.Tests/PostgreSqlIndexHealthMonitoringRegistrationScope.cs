using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Observability.Npgsql.Tests;

internal sealed class PostgreSqlIndexHealthMonitoringRegistrationScope
{
    private readonly ServiceCollection _services = [];

    private PostgreSqlIndexHealthMonitoringRegistrationScope()
    {
    }

    public int HostedServiceCount => _services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService));

    public static PostgreSqlIndexHealthMonitoringRegistrationScope Create(IEnumerable<string> connectionStrings)
    {
        var scope = new PostgreSqlIndexHealthMonitoringRegistrationScope();
        scope.Add(connectionStrings, new PostgreSqlIndexHealthMonitoringOptions());
        return scope;
    }

    public void Add(IEnumerable<string> connectionStrings, PostgreSqlIndexHealthMonitoringOptions options)
    {
        _services.AddPostgreSqlIndexHealthMonitoring(connectionStrings, options);
    }
}
