namespace PandaBear.Testing.Tests;

/// <summary>Wraps a service through a type parameter, which the CallProxy generator cannot see through.</summary>
internal static class CallProxyIndirection
{
    /// <summary>Creates a proxy of a service named only by a type parameter.</summary>
    /// <typeparam name="TService">The service interface.</typeparam>
    /// <param name="inner">The real service.</param>
    /// <returns>The proxy.</returns>
    public static TService Wrap<TService>(TService inner)
        where TService : class =>
        CallProxy.Create(inner);
}
