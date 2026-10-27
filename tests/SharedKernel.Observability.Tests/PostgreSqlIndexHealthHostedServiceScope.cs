using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace PandaBear.Observability.Npgsql.Tests;

/// <summary>Runs the index health hosted service against an unreachable server while recording the telemetry it emits, and stops both on disposal.</summary>
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

    /// <summary>Gets a snapshot of the measurements recorded so far, each with instrument name, value and tags.</summary>
    public IReadOnlyCollection<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)> Measurements => measurements.ToArray();

    /// <summary>Creates the scope and starts its hosted service, which polls a connection string that cannot connect.</summary>
    public static async Task<PostgreSqlIndexHealthHostedServiceScope> Start(CancellationToken ct)
    {
        var scope = new PostgreSqlIndexHealthHostedServiceScope();
        await scope.service.StartAsync(ct);
        return scope;
    }

    /// <summary>Waits up to ten seconds for a collection metric tagged with the "unavailable" outcome and returns true once it is seen.</summary>
    public Task<bool> WaitForUnavailableCollection(CancellationToken ct)
    {
        return unavailableCollectionObserved.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
    }

    /// <inheritdoc />
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
