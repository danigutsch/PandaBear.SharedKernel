namespace PandaBear.Npgsql.Tests;

internal sealed class AsyncDisposableMarker(Exception? disposeFailure = null) : IAsyncDisposable
{
    private int _disposeCount;

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposeCount);
        return disposeFailure is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(disposeFailure);
    }
}
