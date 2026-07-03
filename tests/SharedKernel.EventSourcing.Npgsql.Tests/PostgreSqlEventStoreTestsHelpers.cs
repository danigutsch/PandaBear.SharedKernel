using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.EventSourcing.Npgsql.Tests;

internal static class PostgreSqlEventStoreTestsHelpers
{
    public static PostgreSqlEventSourcingOptions CreateOptions() => new()
    {
        Schema = $"es_{Guid.NewGuid():N}",
    };

    public static async Task<Exception?> CaptureAppendResult(
        PostgreSqlEventStore store,
        StreamId streamId,
        TestEvent eventData)
    {
        try
        {
            await store.Append(
                streamId,
                ExpectedStreamRevision.NoStream,
                [eventData],
                TestContext.Current.CancellationToken);

            return null;
        }
        catch (ExpectedStreamRevisionConflictException exception)
        {
            return exception;
        }
    }

    public static ActivityListener CreateActivityListener(ConcurrentQueue<Activity> stoppedActivities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => string.Equals(
                source.Name,
                PostgreSqlEventSourcingTelemetry.Name,
                StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivities.Enqueue(activity),
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public static MeterListener CreateMeterListener(ConcurrentQueue<string> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) =>
            {
                if (string.Equals(instrument.Meter.Name, PostgreSqlEventSourcingTelemetry.Name, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        listener.Start();
        return listener;
    }

    public static bool HasTag(Activity activity, string key, object expectedValue)
    {
        foreach (var tag in activity.TagObjects)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal) && Equals(tag.Value, expectedValue))
            {
                return true;
            }
        }

        return false;
    }
}
