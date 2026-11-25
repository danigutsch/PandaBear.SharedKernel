using System.ComponentModel;

namespace SharedKernel.Testing;

/// <summary>
/// The base of the hooks that the CallProxy source generator emits for one service interface: it holds the typed hooks of every
/// method and creates the proxy that runs them. Tests use <see cref="CallHooks{TService}"/> instead.
/// </summary>
/// <typeparam name="TService">The service interface.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GeneratedCallHooks<TService>
    where TService : class
{
    /// <summary>Creates a proxy that forwards every call to <paramref name="inner"/> and runs these hooks around it.</summary>
    /// <param name="inner">The real service.</param>
    /// <returns>The proxy.</returns>
    protected internal abstract TService Wrap(TService inner);
}
