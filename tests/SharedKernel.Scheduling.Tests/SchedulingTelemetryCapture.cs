using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PandaBear.Scheduling.Tests;

/// <summary>
/// Captures the polling spans and measurements of one service name, so tests that run in parallel and share the scheduling
/// meter and source see only their own telemetry.
/// </summary>
internal sealed class SchedulingTelemetryCapture : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ConcurrentQueue<(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)> _measurements = new();

    /// <summary>Starts listening to the scheduling source and meter for the given service name.</summary>
    /// <param name="serviceName">The polling service name whose telemetry is captured.</param>
    public SchedulingTelemetryCapture(string serviceName)
    {
        var expectedSource = SchedulingTelemetry.ActivitySource;
        var expectedMeter = SchedulingTelemetry.Meter;
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, expectedSource),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("sharedkernel.scheduling.service.name"), serviceName))
                {
                    _activities.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, expectedMeter))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(serviceName, instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(serviceName, instrument, value, tags));
        _meterListener.Start();
    }

    /// <summary>Gets the stopped polling spans of the service.</summary>
    public IReadOnlyCollection<Activity> Activities => _activities;

    /// <summary>Gets the measurements of the service, with the instrument name and the tags.</summary>
    public IReadOnlyCollection<(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)> Measurements => _measurements;

    /// <inheritdoc />
    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }

    private void Record(string serviceName, Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var recorded = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            recorded[tag.Key] = tag.Value;
        }

        if (Equals(recorded.GetValueOrDefault("sharedkernel.scheduling.service.name"), serviceName))
        {
            _measurements.Enqueue((instrument.Name, value, recorded));
        }
    }
}
