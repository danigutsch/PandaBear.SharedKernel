namespace PandaBear.Npgsql.Tests;

internal sealed class DualDisposableMarker : IAsyncDisposable, IDisposable
{
    private int _asyncDisposeCount;
    private int _disposeCount;

    public int AsyncDisposeCount => Volatile.Read(ref _asyncDisposeCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _asyncDisposeCount);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _ = Interlocked.Increment(ref _disposeCount);
    }
}
