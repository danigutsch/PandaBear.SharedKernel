using static SharedKernel.RepoConfig.Tool.ResourceBudgetGuard;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Completes a simulated process and its cleanup when the guard cancels its token.</summary>
internal sealed class ResourceBudgetTestChild : IDisposable
{
    private readonly TaskCompletionSource<ChildResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _registration;

    /// <summary>Gets whether the simulated child observed its stop.</summary>
    internal bool Stopped => _completion.Task.IsCompletedSuccessfully;

    /// <summary>Waits until the guard stops the simulated child.</summary>
    internal Task<ChildResult> Run(CancellationToken cancellationToken)
    {
        _registration = cancellationToken.Register(() => _completion.TrySetResult(new ChildResult(null, new Cleanup(true, 1, 0))));
        return _completion.Task;
    }

    /// <inheritdoc />
    public void Dispose() => _registration.Dispose();
}
