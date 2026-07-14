using Microsoft.Extensions.Hosting;
using Npgsql;

namespace SharedKernel.Observability.Npgsql;

internal sealed class PostgreSqlIndexHealthHostedService(
    PostgreSqlIndexHealthMonitoringRegistration registration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dataSources = registration.ConnectionStrings
            .Select(connectionString => new NpgsqlDataSourceBuilder(connectionString).Build())
            .ToArray();

        try
        {
            var collectors = dataSources
                .Select(dataSource => new PostgreSqlIndexHealthCollector(dataSource, registration.Options.CommandTimeout))
                .ToArray();
            using var timer = new PeriodicTimer(registration.Options.PollingInterval);

            do
            {
                foreach (var collector in collectors)
                {
                    _ = await collector.Collect(stoppingToken).ConfigureAwait(false);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        finally
        {
            foreach (var dataSource in dataSources)
            {
                await dataSource.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
