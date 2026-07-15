using System.Diagnostics.Metrics;

namespace SharedKernel.Observability.Npgsql.Tests;

internal static class PostgreSqlIndexHealthTelemetryTestListener
{
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
