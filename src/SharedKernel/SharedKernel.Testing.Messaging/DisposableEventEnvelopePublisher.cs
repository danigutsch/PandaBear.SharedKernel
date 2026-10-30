using PandaBear.Messaging;

namespace PandaBear.Testing.Messaging;

/// <summary>
/// Envelope publisher that counts attempts and disposals and can be configured to fail every publish.
/// </summary>
public sealed class DisposableEventEnvelopePublisher : IEventEnvelopePublisher, IDisposable
{
    private int disposeCount;

    /// <summary>
    /// Exception thrown by every publish after the attempt is counted, or null to succeed.
    /// </summary>
    public Exception? Failure
    {
        get; init;
    }

    /// <summary>
    /// Number of publish calls that got past cancellation and argument checks.
    /// </summary>
    public int Attempts
    {
        get; private set;
    }

    /// <summary>
    /// Number of times Dispose has been called.
    /// </summary>
    public int DisposeCount => Volatile.Read(ref disposeCount);

    /// <summary>
    /// Validates the envelope and cancellation token, counts the attempt and throws Failure when it is set.
    /// </summary>
    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();
        Attempts++;

        if (Failure is not null)
        {
            throw Failure;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Increments DisposeCount.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Increment(ref disposeCount);
    }
}
