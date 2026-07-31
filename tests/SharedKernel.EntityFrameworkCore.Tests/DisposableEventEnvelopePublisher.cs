using SharedKernel.Messaging;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class DisposableEventEnvelopePublisher : IEventEnvelopePublisher, IDisposable
{
    private int disposeCount;

    public Exception? Failure { get; init; }

    public int Attempts { get; private set; }

    public int DisposeCount => Volatile.Read(ref disposeCount);

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

    public void Dispose()
    {
        Interlocked.Increment(ref disposeCount);
    }
}
