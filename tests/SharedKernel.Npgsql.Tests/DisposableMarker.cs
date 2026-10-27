namespace PandaBear.Npgsql.Tests;

/// <summary>A synchronously disposable test value that counts disposals and can fail them.</summary>
/// <param name="disposeFailure">The exception thrown from Dispose, or null to dispose without failing.</param>
internal sealed class DisposableMarker(Exception? disposeFailure = null) : IDisposable
{
    private int _disposeCount;

    /// <summary>Gets how many times Dispose has been called.</summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>Counts the disposal and then throws the configured failure, if any.</summary>
    public void Dispose()
    {
        _ = Interlocked.Increment(ref _disposeCount);
        if (disposeFailure is not null)
        {
            throw disposeFailure;
        }
    }
}
