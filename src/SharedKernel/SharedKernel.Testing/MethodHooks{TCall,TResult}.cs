using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SharedKernel.Testing;

/// <summary>
/// The hooks of one method that returns a value, synchronously or as <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>.
/// Hooks run in the order they were added.
/// </summary>
/// <typeparam name="TCall">The generated type that describes one call: a property per argument and the result.</typeparam>
/// <typeparam name="TResult">The result type, the <c>T</c> of <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>.</typeparam>
public sealed class MethodHooks<TCall, TResult>
{
    private readonly CallHookList<TCall> _before = new();
    private readonly CallHookList<TCall> _after = new();
    private bool _replaced;
    private TResult _replacement = default!;

    /// <summary>Runs before the call; an exception stops it, a pending task holds it.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall, TResult> Before(Func<TCall, Task> hook)
    {
        _before.Add(hook);
        return this;
    }

    /// <summary>Runs before the call; an exception stops it.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall, TResult> Before(Action<TCall> hook)
    {
        _before.Add(hook);
        return this;
    }

    /// <summary>Runs after the call completed, with the result set; an exception fails the call after the real service ran.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall, TResult> After(Func<TCall, Task> hook)
    {
        _after.Add(hook);
        return this;
    }

    /// <summary>Runs after the call completed, with the result set; an exception fails the call after the real service ran.</summary>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hook"/> is null.</exception>
    public MethodHooks<TCall, TResult> After(Action<TCall> hook)
    {
        _after.Add(hook);
        return this;
    }

    /// <summary>
    /// Replaces the result; the real service is not called, while before and after hooks still run. A later call replaces the value.
    /// </summary>
    /// <param name="value">The value to return.</param>
    /// <returns>The same hooks.</returns>
    public MethodHooks<TCall, TResult> Returns(TResult value)
    {
        _replacement = value;
        _replaced = true;
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

    /// <summary>Gets the replaced result; the generated proxy calls it.</summary>
    /// <param name="value">The value given to <see cref="Returns"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Returns"/> was called.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetReplacement([MaybeNullWhen(false)] out TResult value)
    {
        value = _replacement;
        return _replaced;
    }
}
