namespace PandaBear.Npgsql.Tests;

/// <summary>An async-disposable test value that counts disposals and can fail them.</summary>
/// <param name="disposeFailure">The exception returned as a faulted task from disposal, or null to complete successfully.</param>
internal sealed class AsyncDisposableMarker(Exception? disposeFailure = null) : IAsyncDisposable
{
    private int _disposeCount;

    /// <summary>Gets how many times DisposeAsync has been called.</summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>Counts the disposal and returns the configured failure as a faulted task, or a completed task when there is none.</summary>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Increment(ref _disposeCount);
        return disposeFailure is null
            ? ValueTask.CompletedTask
            : ValueTask.FromException(disposeFailure);
    }
}
