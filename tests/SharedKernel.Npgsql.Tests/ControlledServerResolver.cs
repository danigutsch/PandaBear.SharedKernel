namespace PandaBear.Npgsql.Tests;

/// <summary>Resolves a server connection string only when the test releases it, and counts the resolutions.</summary>
internal sealed class ControlledServerResolver(string connectionString)
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    /// <summary>Gets how many times Resolve has been called.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gets a task that completes once Resolve has been called, failing after 30 seconds instead of hanging.</summary>
    // A broken implementation must fail the test instead of hanging it.
    public Task Entered => _entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

    /// <summary>Lets pending and later Resolve calls return the connection string.</summary>
    public void Release() => _release.TrySetResult();

    /// <summary>Counts the call, signals that it started, waits for Release or cancellation of the token, then returns the connection string.</summary>
    public async Task<string> Resolve(CancellationToken ct)
    {
        _ = Interlocked.Increment(ref _calls);
        _ = _entered.TrySetResult();
        await _release.Task.WaitAsync(ct);
        return connectionString;
    }
}
