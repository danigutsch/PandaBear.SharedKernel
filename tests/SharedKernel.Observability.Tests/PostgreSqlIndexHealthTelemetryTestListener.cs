using System.Diagnostics.Metrics;

namespace PandaBear.Observability.Npgsql.Tests;

/// <summary>Creates meter listeners that capture long measurements from the PostgreSQL index health meter.</summary>
internal static class PostgreSqlIndexHealthTelemetryTestListener
{
    /// <summary>Starts a listener that enables only the index health meter's instruments and forwards each long measurement, with tags as strings, to the callback.</summary>
    public static MeterListener Create(Action<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)> recordMeasurement)
    {
        ArgumentNullException.ThrowIfNull(recordMeasurement);

        var listener = new MeterListener
        {
            InstrumentPublished = static (instrument, meterListener) =>
            {
                if (string.Equals(instrument.Meter.Name, PostgreSqlIndexHealthTelemetry.MeterName, StringComparison.Ordinal))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var recordedTags = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                recordedTags[tag.Key] = tag.Value?.ToString();
            }

            recordMeasurement((instrument.Name, value, recordedTags));
        });
        listener.Start();
        return listener;
    }
}
