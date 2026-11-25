using System.Diagnostics.Metrics;

namespace SharedKernel.Scheduling.Tests;

/// <summary>Observes duration measurements for a single named test poller.</summary>
internal sealed class PollingMeasurementRecorder : IDisposable
{
    private const string ActiveBatchDuration = "sharedkernel.scheduling.polling.batch.active.duration";

    private readonly MeterListener listener = new();
    private readonly TaskCompletionSource firstCycle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<(double Duration, string? Unit, KeyValuePair<string, object?>[] Tags)> batches = [];

    /// <summary>Listens to the scheduling meter and filters its measurements by the given stable service name.</summary>
    public PollingMeasurementRecorder(string serviceName)
    {
        listener.InstrumentPublished = static (instrument, owner) =>
        {
            if (instrument.Meter.Name == SchedulingTelemetry.Name
                && (instrument.Name == ActiveBatchDuration || instrument.Name == SchedulingTelemetry.MetricPollingCycleDuration))
            {
                owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key != SchedulingTelemetry.TagServiceName || !Equals(tag.Value, serviceName))
                {
                    continue;
                }

                if (instrument is ObservableGauge<double> && instrument.Name == ActiveBatchDuration)
                {
                    batches.Add((value, instrument.Unit, tags.ToArray()));
                }
                else if (instrument.Name == SchedulingTelemetry.MetricPollingCycleDuration)
                {
                    firstCycle.TrySetResult();
                }

                break;
            }
        });
        listener.Start();
    }

    /// <summary>Completes after the first duration measurement from the existing cycle histogram.</summary>
    public Task FirstCompletedCycle => firstCycle.Task;

    /// <summary>Collects the current active-batch gauge observations.</summary>
    public (double Duration, string? Unit, KeyValuePair<string, object?>[] Tags)[] ObserveBatches()
    {
        batches.Clear();
        listener.RecordObservableInstruments();
        return batches.ToArray();
    }

    /// <inheritdoc />
    public void Dispose() => listener.Dispose();
}
