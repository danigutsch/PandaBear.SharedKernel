namespace PandaBear.Npgsql.Tests;

internal sealed class DisposableMarker(Exception? disposeFailure = null) : IDisposable
{
    private int _disposeCount;

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public void Dispose()
    {
        _ = Interlocked.Increment(ref _disposeCount);
        if (disposeFailure is not null)
        {
            throw disposeFailure;
        }
    }
}
