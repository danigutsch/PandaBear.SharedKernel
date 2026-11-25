using PandaBear.Testing.AspNetCore.TestApp;

namespace PandaBear.Testing.AspNetCore.Tests;

/// <summary>Scopes the worker fixture's test hooks and counts disposed host providers.</summary>
internal sealed class WorkerFixtureScope : IDisposable
{
    private readonly Func<Task>? _previousBuild = WorkerFixture.BeforeBuild;
    private readonly Action? _previousDisposal = WorkerFixture.AfterDispose;
    private readonly Action? _releaseWaiters;
    private int _disposals;

    /// <summary>Installs optional construction and disposal checkpoints for this test.</summary>
    internal WorkerFixtureScope(Func<Task>? beforeBuild = null, Action? afterDispose = null, Action? releaseWaiters = null)
    {
        _releaseWaiters = releaseWaiters;
        WorkerFixture.BeforeBuild = beforeBuild;
        WorkerFixture.AfterDispose = () =>
        {
            Interlocked.Increment(ref _disposals);
            afterDispose?.Invoke();
        };
    }

    /// <summary>Gets the number of worker hosts that disposed their provider.</summary>
    internal int Disposals => Volatile.Read(ref _disposals);

    /// <inheritdoc />
    public void Dispose()
    {
        WorkerFixture.BeforeBuild = _previousBuild;
        WorkerFixture.AfterDispose = _previousDisposal;
        _releaseWaiters?.Invoke();
    }
}
