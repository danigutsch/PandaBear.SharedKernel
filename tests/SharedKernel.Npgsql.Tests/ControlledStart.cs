namespace PandaBear.Npgsql.Tests;

/// <summary>Starts nothing by itself: a test completes the start and observes how it was invoked.</summary>
internal sealed class ControlledStart<T>
{
    private readonly TaskCompletionSource<T> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _tokenCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public CancellationToken Token
    {
        get; private set;
    }

    public int ThreadId
    {
        get; private set;
    }

    // A broken implementation must fail the test instead of hanging it.
    public Task TokenCancelled => _tokenCancelled.Task.WaitAsync(TimeSpan.FromSeconds(30));

    public Task<T> Start(CancellationToken ct)
    {
        _ = Interlocked.Increment(ref _calls);
        Token = ct;
        ThreadId = Environment.CurrentManagedThreadId;
        _ = ct.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), _tokenCancelled);
        return _result.Task;
    }

    public void Complete(T value) => _result.SetResult(value);

    public void Fail(Exception failure) => _result.SetException(failure);

    public void Abandon() => _result.SetCanceled();
}
