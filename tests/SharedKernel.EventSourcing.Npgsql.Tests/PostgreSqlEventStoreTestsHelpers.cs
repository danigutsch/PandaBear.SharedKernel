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

    public static async Task<(Activity[] Activities, string[] Measurements)> CaptureTelemetry(
        string connectionString,
        PostgreSqlEventSourcingOptions options,
        string streamId)
    {
        var stoppedActivities = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<string>();
        using var rootActivity = new Activity($"telemetry-isolation-{streamId}");
        _ = rootActivity.Start();
        using var activityListener = CreateActivityListener(stoppedActivities, rootActivity);
        using var meterListener = CreateMeterListener(measurements, options.Schema);
        await using var store = new PostgreSqlEventStore(connectionString, new TestEventSerializer(), options);
        await store.Initialize(TestContext.Current.CancellationToken);
        var id = StreamId.From(streamId);

        await store.Append(
            id,
            ExpectedStreamRevision.NoStream,
            [new TestEvent("created")],
            TestContext.Current.CancellationToken);
        _ = await store.Load(id, afterRevision: null, TestContext.Current.CancellationToken);

        return ([.. stoppedActivities], [.. measurements]);
    }

    public static ActivityListener CreateActivityListener(
        ConcurrentQueue<Activity> stoppedActivities,
        Activity rootActivity)
    {
        ArgumentNullException.ThrowIfNull(rootActivity);
        var traceId = rootActivity.TraceId;
        var parentSpanId = rootActivity.SpanId;
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => string.Equals(
                source.Name,
                PostgreSqlEventSourcingTelemetry.Name,
                StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == traceId && activity.ParentSpanId == parentSpanId)
                {
                    stoppedActivities.Enqueue(activity);
                }
            },
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    public static MeterListener CreateMeterListener(
        ConcurrentQueue<string> measurements,
        string schema)
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

        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (HasMeasurementTag(tags, PostgreSqlEventSourcingTelemetry.TagSchema, schema))
            {
                measurements.Enqueue(instrument.Name);
            }
        });
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (HasMeasurementTag(tags, PostgreSqlEventSourcingTelemetry.TagSchema, schema))
            {
                measurements.Enqueue(instrument.Name);
            }
        });
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

    private static bool HasMeasurementTag(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string key,
        object expectedValue)
    {
        foreach (var tag in tags)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal) && Equals(tag.Value, expectedValue))
            {
                return true;
            }
        }

        return false;
    }
}
