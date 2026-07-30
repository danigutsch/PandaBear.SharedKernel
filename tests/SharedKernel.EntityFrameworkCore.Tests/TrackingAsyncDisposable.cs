namespace SharedKernel.EntityFrameworkCore.Tests;

internal sealed class TrackingAsyncDisposable(
    string name,
    ICollection<string> disposalOrder,
    Exception? failure = null) : IAsyncDisposable
{
    private int disposed;

    internal bool DisposeCalled { get; private set; }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        DisposeCalled = true;
        disposalOrder.Add(name);
        return failure is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(failure);
    }
}
