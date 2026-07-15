using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace SharedKernel.Observability.Npgsql.Tests;

internal sealed class PostgreSqlIndexHealthHostedServiceScope : IAsyncDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);

    private readonly PostgreSqlIndexHealthHostedService service;
    private readonly MeterListener meterListener;
    private readonly TaskCompletionSource<bool> unavailableCollectionObserved;
    private readonly ConcurrentQueue<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)> measurements;

    private PostgreSqlIndexHealthHostedServiceScope()
    {
        var registration = new PostgreSqlIndexHealthMonitoringRegistration(
            ["Host=127.0.0.1;Port=1;Database=monitor;Username=monitor;Password=test-only;Timeout=1"],
            new PostgreSqlIndexHealthMonitoringOptions
            {
                PollingInterval = TimeSpan.FromMinutes(1),
                CommandTimeout = TimeSpan.FromSeconds(1),
            });
        service = new PostgreSqlIndexHealthHostedService(registration);
        measurements = new ConcurrentQueue<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)>();
        unavailableCollectionObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        meterListener = PostgreSqlIndexHealthTelemetryTestListener.Create(RecordMeasurement);
    }

    public IReadOnlyCollection<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)> Measurements => measurements.ToArray();

    public static async Task<PostgreSqlIndexHealthHostedServiceScope> Start(CancellationToken ct)
    {
        var scope = new PostgreSqlIndexHealthHostedServiceScope();
        await scope.service.StartAsync(ct);
        return scope;
    }

    public Task<bool> WaitForUnavailableCollection(CancellationToken ct)
    {
        return unavailableCollectionObserved.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
    }

    public async ValueTask DisposeAsync()
    {
        using var stopTimeout = new CancellationTokenSource(StopTimeout);

        try
        {
            await service.StopAsync(stopTimeout.Token);
        }
        finally
        {
            meterListener.Dispose();
            service.Dispose();
        }
    }

    private void RecordMeasurement((string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags) measurement)
    {
        measurements.Enqueue(measurement);
        if (measurement.InstrumentName == PostgreSqlIndexHealthTelemetry.CollectionMetricName
            && measurement.Tags.TryGetValue("outcome", out var outcome)
            && outcome == "unavailable")
        {
            unavailableCollectionObserved.TrySetResult(true);
        }
    }
}
