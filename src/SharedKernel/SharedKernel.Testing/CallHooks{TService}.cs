using System.ComponentModel;

namespace SharedKernel.Testing;

/// <summary>
/// The hooks of one <see cref="CallProxy"/> over <typeparamref name="TService"/>. Its members are generated: each method of the interface
/// has a member of the same name, such as <c>hooks.Find</c>, that adds <c>Before</c>, <c>After</c> and, for a method with a result,
/// <c>Returns</c> hooks with typed arguments and result.
/// </summary>
/// <typeparam name="TService">The service interface.</typeparam>
public sealed class CallHooks<TService>
    where TService : class
{
    private GeneratedCallHooks<TService>? _generated;

    /// <summary>Creates empty hooks; <see cref="CallProxy.Create"/> and the service collection extensions create them.</summary>
    internal CallHooks()
    {
    }

    /// <summary>Gets the generated hooks of this proxy, creating them on first use; generated members call it.</summary>
    /// <typeparam name="THooks">The generated hooks type.</typeparam>
    /// <returns>The generated hooks.</returns>
    /// <exception cref="InvalidOperationException">The hooks were already configured by code generated in another assembly.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public THooks Generated<THooks>()
        where THooks : GeneratedCallHooks<TService>, new()
    {
        _generated ??= new THooks();
        return _generated as THooks
            ?? throw new InvalidOperationException(
                $"The hooks of {typeof(TService).Name} were configured with members generated in two assemblies; configure them from one assembly.");
    }

    /// <summary>Returns the configured generated hooks, or empty ones when no hook was added.</summary>
    /// <returns>The generated hooks.</returns>
    /// <exception cref="InvalidOperationException">No proxy was generated for <typeparamref name="TService"/>.</exception>
    internal GeneratedCallHooks<TService> Resolve() => _generated ?? CallProxyRegistry<TService>.Create();
}
