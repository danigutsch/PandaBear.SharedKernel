using SharedKernel.Messaging;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class AsyncDisposableEventEnvelopePublisher : IEventEnvelopePublisher, IAsyncDisposable
{
    private int disposeCount;

    public int DisposeCount => Volatile.Read(ref disposeCount);

    public ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref disposeCount);

        return ValueTask.CompletedTask;
    }
}
