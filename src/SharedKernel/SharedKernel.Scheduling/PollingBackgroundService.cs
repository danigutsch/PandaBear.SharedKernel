using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Scheduling;

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
    private readonly BatchDrainExecution drain;
    private readonly Func<CancellationToken, ValueTask<int>> batch;

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
        drain = new BatchDrainExecution(serviceName);
        batch = ExecuteBatch;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        drain.StartObserving(stoppingToken);
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
            drain.StopObserving();
        }
    }

    /// <summary>Runs one complete drain cycle, including its final empty batch.</summary>
    /// <remarks>
    /// Overrides can observe the cycle's start and outcome by awaiting this implementation once.
    /// The polling loop handles exceptions from the override and applies the configured delay when polling continues.
    /// </remarks>
    /// <param name="stoppingToken">The token signaled when the host is shutting down.</param>
    protected virtual ValueTask ExecuteDrainCycle(CancellationToken stoppingToken) => drain.Run(batch, stoppingToken);

    /// <summary>
    /// Processes one batch of currently available work.
    /// </summary>
    /// <param name="stoppingToken">The token signaled when the host is shutting down.</param>
    /// <returns>The number of work items processed by the batch.</returns>
    protected abstract ValueTask<int> ExecuteBatch(CancellationToken stoppingToken);

    private static bool ShouldContinueAfterFailure(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not ThreadAbortException;
    }

    [LoggerMessage(3001, LogLevel.Error, "Polling service {ServiceName} failed while processing background work. Error type: {ErrorType}.", EventName = "PollingCycleFailed")]
    private static partial void LogPollingFailure(ILogger logger, string serviceName, string errorType);
}
