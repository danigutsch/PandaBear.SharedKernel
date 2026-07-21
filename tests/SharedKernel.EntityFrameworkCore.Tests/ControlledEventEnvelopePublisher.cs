using SharedKernel.Messaging;

namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class ControlledEventEnvelopePublisher : IEventEnvelopePublisher, IDisposable
{
    private readonly TaskCompletionSource<bool> firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int disposeCount;
    private int invocationCount;

    public Task FirstStarted => firstStarted.Task;

    public Task SecondStarted => secondStarted.Task;

    public int InvocationCount => Volatile.Read(ref invocationCount);

    public int DisposeCount => Volatile.Read(ref disposeCount);

    public async ValueTask Publish(EventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var invocation = Interlocked.Increment(ref invocationCount);
        switch (invocation)
        {
            case 1:
                firstStarted.TrySetResult(true);
                await releaseFirst.Task.WaitAsync(ct);
                break;
            case 2:
                secondStarted.TrySetResult(true);
                await releaseSecond.Task.WaitAsync(ct);
                break;
            default:
                throw new InvalidOperationException($"Unexpected publish invocation {invocation}.");
        }
    }

    public void ReleaseFirst() => releaseFirst.TrySetResult(true);

    public void ReleaseSecond() => releaseSecond.TrySetResult(true);

    public void Dispose() => Interlocked.CompareExchange(ref disposeCount, 1, 0);
}
