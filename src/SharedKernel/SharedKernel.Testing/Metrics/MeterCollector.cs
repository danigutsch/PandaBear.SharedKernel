using System.Diagnostics.Metrics;

namespace PandaBear.Testing.Metrics;

/// <summary>An <see cref="IMeterFactory" /> that also collects the long measurements of the meters it creates, isolated from other tests.</summary>
public sealed class MeterCollector : IMeterFactory
{
    /// <summary>Gets the message of the exception thrown by the measurement callback while <see cref="ThrowOnMeasurement" /> is set.</summary>
    public const string ListenerFailureMessage = "listener-secret";

    private readonly List<Meter> _meters = [];
    private readonly MeterListener _listener = new();
    private readonly List<RecordedMeasurement> _measurements = [];

    /// <summary>Initializes a new instance of the <see cref="MeterCollector" /> class and starts listening.</summary>
    /// <param name="meterName">The name of the only meter to collect, or <see langword="null" /> to collect every meter this factory creates.</param>
    public MeterCollector(string? meterName = null)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, this) && (meterName is null || instrument.Meter.Name == meterName))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (ThrowOnMeasurement)
            {
                throw new InvalidOperationException(ListenerFailureMessage);
            }

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

    /// <summary>Gets or sets a value indicating whether the measurement callback throws, to simulate a faulty listener.</summary>
    public bool ThrowOnMeasurement
    {
        get; set;
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
