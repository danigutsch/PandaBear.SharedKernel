namespace SharedKernel.Testing;

/// <summary>Holds the factory of the generated hooks of one service interface, registered by a generated module initializer.</summary>
/// <typeparam name="TService">The service interface.</typeparam>
internal static class CallProxyRegistry<TService>
    where TService : class
{
    private static Func<GeneratedCallHooks<TService>>? _create;

    /// <summary>Keeps the first factory; another assembly that generated the same interface creates equivalent hooks.</summary>
    /// <param name="create">Creates empty generated hooks.</param>
    internal static void Register(Func<GeneratedCallHooks<TService>> create) =>
        Interlocked.CompareExchange(ref _create, create, null);

    /// <summary>Creates empty generated hooks.</summary>
    /// <returns>The hooks.</returns>
    /// <exception cref="InvalidOperationException">No generated hooks were registered for <typeparamref name="TService"/>.</exception>
    internal static GeneratedCallHooks<TService> Create()
    {
        var create = Volatile.Read(ref _create);
        return create is not null
            ? create()
            : throw new InvalidOperationException(
                $"CallProxy has no generated proxy for {typeof(TService).Name}. Name the interface in CallProxy.Create<{typeof(TService).Name}> or "
                + $"WrapWithCallProxy<{typeof(TService).Name}>, and reference PandaBear.SharedKernel.Testing directly from the test project so its "
                + "source generator runs.");
    }
}
