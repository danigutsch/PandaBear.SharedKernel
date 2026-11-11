namespace PandaBear.ServiceDefaults.Tests;

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
}
