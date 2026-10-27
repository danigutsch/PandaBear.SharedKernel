namespace PandaBear.Npgsql.Tests;

/// <summary>An async-disposable value whose disposal waits until a test lets it finish, and then fails.</summary>
/// <param name="failure">What the disposal throws once it is allowed to finish.</param>
internal sealed class BlockedDisposal(Exception failure) : IAsyncDisposable
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _mayFinish = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes once disposal has started, failing after 30 seconds instead of hanging.</summary>
    // A broken implementation must fail the test instead of hanging it.
    public Task Entered => _entered.Task.WaitAsync(TimeSpan.FromSeconds(30));

    /// <summary>Lets the blocked disposal continue and throw its failure.</summary>
    public void Finish() => _mayFinish.SetResult();

    /// <summary>Signals that disposal has started, waits until Finish is called, then throws the configured failure.</summary>
    // A second disposal of the value fails here, because the signal below can be set only once.
    public async ValueTask DisposeAsync()
    {
        _entered.SetResult();
        await _mayFinish.Task.ConfigureAwait(false);
        throw failure;
    }
}
