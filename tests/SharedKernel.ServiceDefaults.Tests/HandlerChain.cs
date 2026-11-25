namespace SharedKernel.ServiceDefaults.Tests;

/// <summary>Lists the handlers of an HttpClient pipeline.</summary>
internal static class HandlerChain
{
    /// <summary>Walks a handler and its inner handlers.</summary>
    /// <param name="handler">The outermost handler.</param>
    /// <returns>The handlers, outermost first.</returns>
    internal static List<HttpMessageHandler> Of(HttpMessageHandler handler)
    {
        var chain = new List<HttpMessageHandler>();
        for (HttpMessageHandler? current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
        {
            chain.Add(current);
        }

        return chain;
    }

    /// <summary>Tells whether a handler is the service discovery handler, whose type is internal to its package.</summary>
    /// <param name="handler">The handler.</param>
    /// <returns><see langword="true"/> for the service discovery handler.</returns>
    internal static bool IsServiceDiscovery(HttpMessageHandler handler) =>
        handler.GetType().FullName == "Microsoft.Extensions.ServiceDiscovery.Http.ResolvingHttpDelegatingHandler";
}
