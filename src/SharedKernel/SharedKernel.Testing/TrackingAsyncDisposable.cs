namespace PandaBear.Testing;

/// <summary>An async-disposable that records its disposal order once, optionally returning a faulted task, to test disposal sequencing and failure handling.</summary>
/// <param name="name">The name appended to <paramref name="disposalOrder" /> when this instance is disposed.</param>
/// <param name="disposalOrder">The shared list that receives <paramref name="name" /> on first disposal.</param>
/// <param name="failure">The exception returned as a faulted task from disposal, or <see langword="null" /> to complete successfully.</param>
public sealed class TrackingAsyncDisposable(
    string name,
    ICollection<string> disposalOrder,
    Exception? failure = null) : IAsyncDisposable
{
    private int disposed;

    /// <summary>Gets a value indicating whether the first disposal call has run.</summary>
    public bool DisposeCalled
    {
        get; private set;
    }

    /// <summary>Records the name once and returns the configured failure; repeat calls complete immediately without recording again.</summary>
    /// <returns>A completed task, or a faulted task carrying the configured failure on the first call.</returns>
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
