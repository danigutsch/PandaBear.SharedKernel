namespace SharedKernel.Testing;

/// <summary>The hooks of one phase of one method, run in the order they were added.</summary>
/// <typeparam name="TCall">The generated type that describes one call.</typeparam>
internal sealed class CallHookList<TCall>
{
    private readonly List<Func<TCall, Task>> _hooks = [];

    /// <summary>Adds an asynchronous hook.</summary>
    /// <param name="hook">The hook.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    internal void Add(Func<TCall, Task> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        _hooks.Add(hook);
    }

    /// <summary>Adds a synchronous hook.</summary>
    /// <param name="hook">The hook.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    internal void Add(Action<TCall> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        _hooks.Add(call =>
        {
            hook(call);
            return Task.CompletedTask;
        });
    }

    /// <summary>Runs the hooks one after the other.</summary>
    /// <param name="call">The call.</param>
    /// <returns>A task that completes when every hook has completed; a hook that throws stops the rest.</returns>
    internal Task Run(TCall call) => _hooks.Count == 0 ? Task.CompletedTask : RunAll(call);

    private async Task RunAll(TCall call)
    {
        foreach (var hook in _hooks)
        {
            await hook(call).ConfigureAwait(false);
        }
    }
}
