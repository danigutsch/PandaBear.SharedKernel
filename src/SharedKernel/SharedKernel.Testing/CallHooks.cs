namespace PandaBear.Testing;

/// <summary>
/// The hooks of one <see cref="CallProxy"/>, chosen by method name. Hooks of a method run in the order they were added.
/// A name applies to every overload of that name. <see cref="CallProxy.Create{TService}"/> rejects a name that matches no method.
/// </summary>
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

    /// <summary>Checks every registered name against the methods of the service and its inherited interfaces.</summary>
    /// <param name="service">The service interface.</param>
    /// <exception cref="ArgumentException">A name matches no method.</exception>
    /// <exception cref="InvalidOperationException">A replacement is registered where it cannot work.</exception>
    internal void Validate(Type service)
    {
        var methods = service.GetMethods().Concat(service.GetInterfaces().SelectMany(static i => i.GetMethods())).ToList();
        var registered = BeforeCalls.Keys.Concat(AfterCalls.Keys).Concat(Replacements.Keys).Distinct(StringComparer.Ordinal).ToList();
        var unknown = registered.Where(name => !methods.Exists(m => m.Name == name)).Order(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            var available = string.Join(", ", methods.Select(static m => m.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            throw new ArgumentException(
                $"CallProxy has no method named {string.Join(", ", unknown.Select(static n => $"'{n}'"))} on {service.Name}. Available methods: {available}.",
                nameof(service));
        }

        foreach (var name in Replacements.Keys)
        {
            var named = methods.Where(m => m.Name == name).ToList();
            var valued = named.Where(static m => ReturnsValue(m.ReturnType)).ToList();
            if (valued.Count == 0)
            {
                throw new InvalidOperationException($"Returns cannot be used for '{name}', which returns no value.");
            }

            if (valued.TrueForAll(static m => m.GetParameters().Any(static p => p.ParameterType.IsByRef)))
            {
                throw new InvalidOperationException(
                    $"Returns cannot be used for '{name}': out/ref parameters cannot be stubbed. Use After or Before and the real call instead.");
            }
        }
    }

    private static bool ReturnsValue(Type returnType)
    {
        if (returnType == typeof(void) || returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            return false;
        }

        return true;
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
