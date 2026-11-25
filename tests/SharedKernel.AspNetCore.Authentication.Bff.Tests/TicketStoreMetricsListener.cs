using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace SharedKernel.AspNetCore.Authentication.Bff.Tests;

/// <summary>Collects package-wide meter measurements while a ticket store test runs.</summary>
internal sealed class TicketStoreMetricsListener : IDisposable
{
    private const string MeterName = "SharedKernel.AspNetCore.Authentication.Bff";
    private const string CounterName = "sharedkernel.bff.session_ticket.failures";

    private readonly MeterListener _listener;
    private readonly ConcurrentQueue<(string Name, string? Unit, long Value, KeyValuePair<string, object?>[] Tags)> _measurements = new();

    /// <summary>Starts a listener for every instrument in the ticket store package.</summary>
    internal TicketStoreMetricsListener(bool throwOnMeasurement = false)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = static (instrument, observer) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    observer.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            _measurements.Enqueue((instrument.Name, instrument.Unit, measurement, tags.ToArray()));
            if (throwOnMeasurement)
            {
                throw new InvalidOperationException("The metrics listener failed.");
            }
        });
        _listener.Start();
    }

    /// <summary>Verifies that one increment was emitted with the operation as its only dimension.</summary>
    internal void ShouldHaveSingleFailure(string operation)
    {
        var measurement = _measurements.ShouldHaveSingleItem();
        measurement.Name.ShouldBe(CounterName);
        measurement.Unit.ShouldNotBeNull().ShouldBe("{failure}");
        measurement.Value.ShouldBe(1);
        var tag = measurement.Tags.ShouldHaveSingleItem();
        tag.Key.ShouldBe("sharedkernel.bff.session_ticket.operation");
        tag.Value.ShouldBe(operation);
    }

    /// <summary>Verifies that the package meter emitted no measurements.</summary>
    internal void ShouldHaveNoMeasurements() => _measurements.ShouldBeEmpty();

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();
}
