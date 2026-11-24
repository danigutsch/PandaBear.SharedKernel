using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PandaBear.Scheduling;

/// <summary>
/// Runs batches of background work until the queue is drained, then waits before polling again.
/// </summary>
/// <remarks>
/// This base class is intended for lightweight, host-owned polling loops. It is not a durable
/// scheduler, queue, or job store by itself; implementations remain responsible for persistence,
/// locking, idempotency, and business-specific retry state.
/// </remarks>
public abstract partial class PollingBackgroundService : BackgroundService
{
    private readonly ILogger logger;
    private readonly string serviceName;
    private readonly TimeSpan pollInterval;
    private long batchStartedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingBackgroundService" /> class.
    /// </summary>
    /// <param name="logger">The logger used for polling failures.</param>
    /// <param name="serviceName">The stable, low-cardinality name used for the polling service's telemetry.</param>
    /// <param name="pollInterval">The delay after all currently available work has been drained.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="serviceName" /> is blank.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pollInterval" /> is less than or equal to <see cref="TimeSpan.Zero" />.
    /// </exception>
    protected PollingBackgroundService(ILogger logger, string serviceName, TimeSpan pollInterval)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Poll interval must be greater than zero.");
        }

        this.logger = logger;
        this.serviceName = serviceName;
        this.pollInterval = pollInterval;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SchedulingTelemetry.RegisterPollingService(this, serviceName, stoppingToken);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteDrainCycle(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (ShouldContinueAfterFailure(exception))
                {
                    var errorType = exception.GetType();
                    SchedulingTelemetry.PollingFailures.Add(
                        1,
                        new KeyValuePair<string, object?>(SchedulingTelemetry.TagServiceName, serviceName),
                        new KeyValuePair<string, object?>(SchedulingTelemetry.TagErrorType, errorType.FullName));
                    LogPollingFailure(logger, serviceName, errorType.FullName ?? errorType.Name);
                }

                using var timer = new PeriodicTimer(pollInterval);
                _ = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            SchedulingTelemetry.UnregisterPollingService(this);
        }
    }

    /// <summary>Gets the active batch's age in seconds, or zero between batches.</summary>
    internal double GetActiveBatchDurationSeconds()
    {
        var startedAt = Volatile.Read(ref batchStartedAt);
        return startedAt == 0 ? 0 : Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
    }

    /// <summary>Runs one complete drain cycle, including its final empty batch.</summary>
    /// <remarks>
    /// Overrides can observe the cycle's start and outcome by awaiting this implementation once.
    /// The polling loop handles exceptions from the override and applies the configured delay when polling continues.
    /// </remarks>
    /// <param name="stoppingToken">The token signaled when the host is shutting down.</param>
    protected virtual async ValueTask ExecuteDrainCycle(CancellationToken stoppingToken)
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
                processed = await ExecuteTrackedBatch(stoppingToken).ConfigureAwait(false);
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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

    /// <summary>
    /// Processes one batch of currently available work.
    /// </summary>
    /// <param name="stoppingToken">The token signaled when the host is shutting down.</param>
    /// <returns>The number of work items processed by the batch.</returns>
    protected abstract ValueTask<int> ExecuteBatch(CancellationToken stoppingToken);

    private async ValueTask<int> ExecuteTrackedBatch(CancellationToken stoppingToken)
    {
        Volatile.Write(ref batchStartedAt, Stopwatch.GetTimestamp());
        try
        {
            return await ExecuteBatch(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref batchStartedAt, 0);
        }
    }

    private static bool ShouldContinueAfterFailure(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not ThreadAbortException;
    }

    [LoggerMessage(3001, LogLevel.Error, "Polling service {ServiceName} failed while processing background work. Error type: {ErrorType}.", EventName = "PollingCycleFailed")]
    private static partial void LogPollingFailure(ILogger logger, string serviceName, string errorType);
}
