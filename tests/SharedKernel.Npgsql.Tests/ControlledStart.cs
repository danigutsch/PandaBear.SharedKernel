namespace PandaBear.Npgsql.Tests;

/// <summary>Starts nothing by itself: a test completes the start and observes how it was invoked.</summary>
/// <param name="cancellationCallbackFailure">
/// When set, the start registers a callback on its token that throws this failure when the token is cancelled.
/// </param>
internal sealed class ControlledStart<T>(Exception? cancellationCallbackFailure = null)
{
    private readonly TaskCompletionSource<T> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _tokenCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    /// <summary>Gets how many times Start has been called.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets the token passed to the most recent Start call.</summary>
    public CancellationToken Token
    {
        get; private set;
    }

    /// <summary>Gets the managed thread id on which the most recent Start call ran.</summary>
    public int ThreadId
    {
        get; private set;
    }

    /// <summary>Gets a task that completes when the token given to Start is cancelled, failing after 30 seconds instead of hanging.</summary>
    // A broken implementation must fail the test instead of hanging it.
    public Task TokenCancelled => _tokenCancelled.Task.WaitAsync(TimeSpan.FromSeconds(30));

    /// <summary>Records the call, token and thread, registers the cancellation callbacks, and returns a task that stays pending until Complete, Fail or Abandon is called.</summary>
    public Task<T> Start(CancellationToken ct)
    {
        _ = Interlocked.Increment(ref _calls);
        Token = ct;
        ThreadId = Environment.CurrentManagedThreadId;
        _ = ct.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), _tokenCancelled);
        if (cancellationCallbackFailure is not null)
        {
            // A throwing callback does not stop the other callbacks of the same token from running.
            _ = ct.Register(static state => throw (Exception)state!, cancellationCallbackFailure);
        }

        return _result.Task;
    }

    /// <summary>Completes the pending start with the given value.</summary>
    public void Complete(T value) => _result.SetResult(value);

    /// <summary>Faults the pending start with the given exception.</summary>
    public void Fail(Exception failure) => _result.SetException(failure);

    /// <summary>Cancels the pending start task.</summary>
    public void Abandon() => _result.SetCanceled();
}
