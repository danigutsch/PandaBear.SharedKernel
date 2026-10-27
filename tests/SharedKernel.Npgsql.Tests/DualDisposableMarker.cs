namespace PandaBear.Npgsql.Tests;

/// <summary>A test value that is both async and sync disposable and counts calls to each, to show which one a caller uses.</summary>
internal sealed class DualDisposableMarker : IAsyncDisposable, IDisposable
{
    private int _asyncDisposeCount;
    private int _disposeCount;

    /// <summary>Gets how many times DisposeAsync has been called.</summary>
    public int AsyncDisposeCount => Volatile.Read(ref _asyncDisposeCount);

    /// <summary>Gets how many times Dispose has been called.</summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>Counts the asynchronous disposal and completes successfully.</summary>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _asyncDisposeCount);
        return ValueTask.CompletedTask;
    }

    /// <summary>Counts the synchronous disposal.</summary>
    public void Dispose()
    {
        _ = Interlocked.Increment(ref _disposeCount);
    }
}
