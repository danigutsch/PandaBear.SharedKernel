namespace SharedKernel.Scheduling;

/// <summary>Drains available work in batches and observes the active batch for the lifetime of this instance.</summary>
/// <remarks>
/// Use a separate instance for each concurrently running drain. Instances with the same stable service name
/// share one gauge series reporting the longest active batch. The caller owns when to run, cancel, and retry.
/// </remarks>
public sealed class BatchDrain : IDisposable
{
    private const int Idle = 0;
    private const int Running = 1;
    private const int Disposed = 2;

    private readonly BatchDrainExecution execution;
    private int state;

    /// <summary>Begins observing a named drain, including when it is idle between runs.</summary>
    /// <param name="serviceName">A stable, low-cardinality name chosen by the caller.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName" /> is blank.</exception>
    public BatchDrain(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        execution = new BatchDrainExecution(serviceName);
        execution.StartObserving(CancellationToken.None);
    }

    /// <summary>Runs batches through the first result that reports no processed items.</summary>
    /// <param name="executeBatch">Processes one batch and returns the number of items processed.</param>
    /// <param name="ct">The caller's cancellation token, forwarded to every batch.</param>
    /// <returns>A task that completes after the drain ends and propagates batch failures and cancellation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="executeBatch" /> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a drain is already running on this instance.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance has been disposed.</exception>
    public async ValueTask Run(Func<CancellationToken, ValueTask<int>> executeBatch, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(executeBatch);
        var previous = Interlocked.CompareExchange(ref state, Running, Idle);
        ObjectDisposedException.ThrowIf(previous == Disposed, this);
        if (previous == Running)
        {
            throw new InvalidOperationException("This batch drain is already running.");
        }

        try
        {
            await execution.Run(executeBatch, ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref state, Idle, Running);
        }
    }

    /// <summary>Removes this drain's observation. Active work remains owned by its caller.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref state, Disposed);
        execution.StopObserving();
    }
}
