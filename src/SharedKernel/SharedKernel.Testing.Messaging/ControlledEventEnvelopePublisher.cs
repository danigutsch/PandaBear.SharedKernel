using PandaBear.Messaging;

namespace PandaBear.Testing.Messaging;

/// <summary>
/// Envelope publisher that blocks its first two publish calls until the test releases them, so ordering of consumption can be observed.
/// </summary>
public sealed class ControlledEventEnvelopePublisher : IEventEnvelopePublisher, IDisposable
{
    private readonly TaskCompletionSource<bool> firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int disposeCount;
    private int invocationCount;

    /// <summary>
    /// Task that completes once the first publish call has started.
    /// </summary>
    public Task FirstStarted => firstStarted.Task;

    /// <summary>
    /// Task that completes once the second publish call has started.
    /// </summary>
    public Task SecondStarted => secondStarted.Task;

    /// <summary>
    /// Number of Publish calls started so far.
    /// </summary>
    public int InvocationCount => Volatile.Read(ref invocationCount);

    /// <summary>
    /// Number of times Dispose has been called.
    /// </summary>
    public int DisposeCount => Volatile.Read(ref disposeCount);

    /// <summary>
    /// Counts the invocation, signals that it started and waits until it is released; a third call throws InvalidOperationException.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="envelope"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown on the third and every later call.</exception>
    public async ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var invocation = Interlocked.Increment(ref invocationCount);
        switch (invocation)
        {
            case 1:
                firstStarted.TrySetResult(true);
                await releaseFirst.Task.WaitAsync(ct).ConfigureAwait(false);
                break;
            case 2:
                secondStarted.TrySetResult(true);
                await releaseSecond.Task.WaitAsync(ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unexpected publish invocation {invocation}.");
        }
    }

    /// <summary>
    /// Lets the blocked first publish call finish.
    /// </summary>
    public void ReleaseFirst() => releaseFirst.TrySetResult(true);

    /// <summary>
    /// Lets the blocked second publish call finish.
    /// </summary>
    public void ReleaseSecond() => releaseSecond.TrySetResult(true);

    /// <summary>
    /// Increments DisposeCount.
    /// </summary>
    public void Dispose() => Interlocked.Increment(ref disposeCount);
}
