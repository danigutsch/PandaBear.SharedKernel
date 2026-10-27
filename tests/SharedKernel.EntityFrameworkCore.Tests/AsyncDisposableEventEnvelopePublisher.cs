using PandaBear.Messaging;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Envelope publisher that ignores published envelopes and counts how often it is asynchronously disposed.
/// </summary>
internal sealed class AsyncDisposableEventEnvelopePublisher : IEventEnvelopePublisher, IAsyncDisposable
{
    private int disposeCount;

    /// <summary>
    /// Number of times DisposeAsync has been called.
    /// </summary>
    public int DisposeCount => Volatile.Read(ref disposeCount);

    /// <summary>
    /// Validates the envelope and cancellation token, then completes without publishing anything.
    /// </summary>
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Increments DisposeCount.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref disposeCount);

        return ValueTask.CompletedTask;
    }
}
