using System.ComponentModel;

namespace PandaBear.Testing;

/// <summary>
/// Wraps a service so a test can hold a call, make it fail, follow it up, or replace its result, without writing a decorator class per service.
/// </summary>
/// <remarks>
/// The proxy and its typed hooks are generated at compile time by the source generator that ships in this package, for every interface
/// named in <c>CallProxy.Create&lt;T&gt;</c> or <c>WrapWithCallProxy&lt;T&gt;</c> or used as <c>hooks.Method.Before(...)</c>. Each method of
/// the interface gets a hooks member of the same name, such as <c>hooks.Find</c>, with typed arguments and result. Hooks of a synchronous
/// method are waited for on the calling thread, so a hook that stays pending blocks that call; use it only with tasks that complete
/// without the caller's help.
/// </remarks>
public static class CallProxy
{
    /// <summary>Creates a proxy of <typeparamref name="TService"/> over a real implementation.</summary>
    /// <typeparam name="TService">The service interface.</typeparam>
    /// <param name="inner">The real service.</param>
    /// <param name="configure">Adds the hooks, or <see langword="null"/> for a plain pass-through.</param>
    /// <returns>The proxy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="InvalidOperationException">No proxy was generated for <typeparamref name="TService"/>.</exception>
    public static TService Create<TService>(TService inner, Action<CallHooks<TService>>? configure = null)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(inner);

        var hooks = new CallHooks<TService>();
        configure?.Invoke(hooks);
        return hooks.Resolve().Wrap(inner);
    }

    /// <summary>Registers the generated hooks of <typeparamref name="TService"/>; generated code calls it from a module initializer.</summary>
    /// <typeparam name="TService">The service interface.</typeparam>
    /// <param name="create">Creates empty generated hooks, used when a test adds none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> is null.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register<TService>(Func<GeneratedCallHooks<TService>> create)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(create);

        CallProxyRegistry<TService>.Register(create);
    }
}
