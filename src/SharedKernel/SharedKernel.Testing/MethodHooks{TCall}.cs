using System.ComponentModel;

namespace SharedKernel.Testing;

/// <summary>
/// The hooks of one method that returns no value: <see langword="void"/>, <see cref="Task"/> or <see cref="ValueTask"/>. Hooks run in
/// the order they were added.
/// </summary>
/// <typeparam name="TCall">The generated type that describes one call: a property per argument.</typeparam>
public sealed class MethodHooks<TCall>
{
    private readonly CallHookList<TCall> _before = new();
    private readonly CallHookList<TCall> _after = new();

    /// <summary>Runs before the call; an exception stops it, a pending task holds it.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall> Before(Func<TCall, Task> hook)
    {
        _before.Add(hook);
        return this;
    }

    /// <summary>Runs before the call; an exception stops it.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall> Before(Action<TCall> hook)
    {
        _before.Add(hook);
        return this;
    }

    /// <summary>Runs after the call completed; an exception fails the call after the real service ran.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall> After(Func<TCall, Task> hook)
    {
        _after.Add(hook);
        return this;
    }

    /// <summary>Runs after the call completed; an exception fails the call after the real service ran.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall> After(Action<TCall> hook)
    {
        _after.Add(hook);
        return this;
    }

    /// <summary>Runs the before hooks; the generated proxy calls it.</summary>
    /// <param name="call">The call.</param>
    /// <returns>A task that completes when every before hook has completed.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task RunBefore(TCall call) => _before.Run(call);

    /// <summary>Runs the after hooks; the generated proxy calls it.</summary>
    /// <param name="call">The call.</param>
    /// <returns>A task that completes when every after hook has completed.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task RunAfter(TCall call) => _after.Run(call);
}
