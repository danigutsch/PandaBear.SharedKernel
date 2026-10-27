using System.Diagnostics.Metrics;

namespace PandaBear.AspNetCore.Tests;

/// <summary>A captured counter measurement.</summary>
/// <param name="Instrument">The instrument name.</param>
/// <param name="Value">The measured value.</param>
/// <param name="Tags">The tags by name.</param>
internal sealed record RecordedMeasurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);

/// <summary>An <see cref="IMeterFactory" /> that also collects the measurements of the meters it creates, isolated from other tests.</summary>
internal sealed class MeterCollector : IMeterFactory
{
    private readonly List<Meter> _meters = [];
    private readonly MeterListener _listener = new();
    private readonly List<RecordedMeasurement> _measurements = [];

    /// <summary>Initializes a new instance of the <see cref="MeterCollector" /> class and starts listening.</summary>
    public MeterCollector()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, this) && instrument.Meter.Name == AspNetCoreTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                map[tag.Key] = tag.Value;
            }

            lock (_measurements)
            {
                _measurements.Add(new RecordedMeasurement(instrument.Name, value, map));
            }
        });
        _listener.Start();
    }

    /// <summary>Gets the measurements captured so far.</summary>
    public IReadOnlyList<RecordedMeasurement> Measurements => _measurements;

    /// <inheritdoc />
    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });
        _meters.Add(meter);
        return meter;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _listener.Dispose();
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }
    }
}
