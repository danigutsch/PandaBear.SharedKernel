using System.Diagnostics;

namespace SharedKernel.Scheduling;

/// <summary>Runs and observes batches independently of the owner's scheduling and registration lifetime.</summary>
/// <param name="serviceName">The stable service name used for telemetry.</param>
internal sealed class BatchDrainExecution(string serviceName)
{
    private long batchStartedAt;

    /// <summary>Registers the active-batch measurement for the owner's lifetime.</summary>
    internal void StartObserving(CancellationToken stoppingToken) =>
        SchedulingTelemetry.RegisterBatchDrain(this, serviceName, stoppingToken);

    /// <summary>Removes the owner's active-batch measurement.</summary>
    internal void StopObserving() => SchedulingTelemetry.UnregisterBatchDrain(this);

    /// <summary>Gets the active batch's age in seconds, or zero between batches.</summary>
    internal double GetActiveBatchDurationSeconds()
    {
        var startedAt = Volatile.Read(ref batchStartedAt);
        return startedAt == 0 ? 0 : Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
    }

    /// <summary>Drains available work and emits the shared cycle, batch, and item telemetry.</summary>
    internal async ValueTask Run(Func<CancellationToken, ValueTask<int>> executeBatch, CancellationToken ct)
    {
        using var activity = SchedulingTelemetry.ActivitySource.StartActivity(
            SchedulingTelemetry.ActivityPollingCycle,
            ActivityKind.Internal);
        activity?.SetTag(SchedulingTelemetry.TagServiceName, serviceName);

        var startedAt = Stopwatch.GetTimestamp();
        var totalProcessed = 0;
        var outcome = SchedulingTelemetry.OutcomeSuccess;
        try
        {
            int processed;
            do
            {
                processed = await ExecuteTrackedBatch(executeBatch, ct).ConfigureAwait(false);
                SchedulingTelemetry.PollingBatches.Add(1, new KeyValuePair<string, object?>(SchedulingTelemetry.TagServiceName, serviceName));
                if (processed > 0)
                {
                    SchedulingTelemetry.PollingItems.Add(processed, new KeyValuePair<string, object?>(SchedulingTelemetry.TagServiceName, serviceName));
                    totalProcessed += processed;
                }
            }
            while (processed > 0);
            activity?.SetTag(SchedulingTelemetry.TagOutcome, outcome);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            outcome = SchedulingTelemetry.OutcomeCancelled;
            activity?.SetTag(SchedulingTelemetry.TagOutcome, outcome);
            throw;
        }
        catch (Exception exception)
        {
            outcome = SchedulingTelemetry.OutcomeError;
            activity?.SetTag(SchedulingTelemetry.TagOutcome, outcome);
            activity?.SetTag(SchedulingTelemetry.TagErrorType, exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
        finally
        {
            var durationSeconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
            activity?.SetTag(SchedulingTelemetry.TagItemCount, totalProcessed);
            SchedulingTelemetry.PollingCycleDuration.Record(
                durationSeconds,
                new KeyValuePair<string, object?>(SchedulingTelemetry.TagServiceName, serviceName),
                new KeyValuePair<string, object?>(SchedulingTelemetry.TagOutcome, outcome));
        }
    }

    private async ValueTask<int> ExecuteTrackedBatch(Func<CancellationToken, ValueTask<int>> executeBatch, CancellationToken ct)
    {
        Volatile.Write(ref batchStartedAt, Stopwatch.GetTimestamp());
        try
        {
            return await executeBatch(ct).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref batchStartedAt, 0);
        }
    }
}
