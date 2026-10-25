namespace PandaBear.Npgsql.Tests;

/// <summary>Resolves a server connection string only when the test releases it, and counts the resolutions.</summary>
internal sealed class ControlledServerResolver(string connectionString)
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    // A broken implementation must fail the test instead of hanging it.
    public Task Entered => _entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

    public void Release() => _release.TrySetResult();

    public async Task<string> Resolve(CancellationToken ct)
    {
        _ = Interlocked.Increment(ref _calls);
        _ = _entered.TrySetResult();
        await _release.Task.WaitAsync(ct);
        return connectionString;
    }
}
