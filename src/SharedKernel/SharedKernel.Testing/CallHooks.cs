namespace PandaBear.Testing;

/// <summary>The hooks of one <see cref="CallProxy"/>, chosen by method name. Hooks of a method run in the order they were added.</summary>
public sealed class CallHooks
{
    /// <summary>Gets the hooks that run before a call.</summary>
    internal Dictionary<string, List<Func<CallInfo, Task>>> BeforeCalls { get; } = [];

    /// <summary>Gets the hooks that run after a call.</summary>
    internal Dictionary<string, List<Func<CallInfo, Task>>> AfterCalls { get; } = [];

    /// <summary>Gets the replaced results.</summary>
    internal Dictionary<string, object?> Replacements { get; } = [];

    /// <summary>Runs before the call; an exception stops it, a pending task holds it.</summary>
    /// <param name="method">The method name.</param>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    public CallHooks Before(string method, Func<CallInfo, Task> hook) => Add(BeforeCalls, method, hook);

    /// <summary>Runs after the call, with <see cref="CallInfo.Result"/> set.</summary>
    /// <param name="method">The method name.</param>
    /// <param name="hook">The hook.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException">A value is null.</exception>
    public CallHooks After(string method, Func<CallInfo, Task> hook) => Add(AfterCalls, method, hook);

    /// <summary>Replaces the result of a method that returns a value; the real service is not called.</summary>
    /// <param name="method">The method name.</param>
    /// <param name="value">The value to return.</param>
    /// <returns>The same hooks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> is null.</exception>
    public CallHooks Returns(string method, object? value)
    {
        ArgumentNullException.ThrowIfNull(method);

        Replacements[method] = value;
        return this;
    }

    /// <summary>Runs the hooks of the called method in the order they were added.</summary>
    /// <param name="hooks">The hooks of one phase.</param>
    /// <param name="call">The call.</param>
    /// <returns>A task that completes when all hooks have run.</returns>
    internal static async Task Run(Dictionary<string, List<Func<CallInfo, Task>>> hooks, CallInfo call)
    {
        if (hooks.TryGetValue(call.Method, out var list))
        {
            foreach (var hook in list)
            {
                await hook(call).ConfigureAwait(false);
            }
        }
    }

    private CallHooks Add(Dictionary<string, List<Func<CallInfo, Task>>> hooks, string method, Func<CallInfo, Task> hook)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(hook);

        if (!hooks.TryGetValue(method, out var list))
        {
            hooks[method] = list = [];
        }

        list.Add(hook);
        return this;
    }
}
