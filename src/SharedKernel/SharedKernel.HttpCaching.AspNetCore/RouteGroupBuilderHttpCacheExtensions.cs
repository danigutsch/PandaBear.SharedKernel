using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SharedKernel.HttpCaching.AspNetCore;

/// <summary>
/// Adds HTTP cache-safety policies to Minimal API route groups.
/// </summary>
public static class RouteGroupBuilderHttpCacheExtensions
{
    /// <summary>
    /// Applies private no-store response headers to every executed endpoint in a route group.
    /// </summary>
    /// <param name="group">The route group to protect from client and intermediary caching.</param>
    /// <returns>The same route group for fluent endpoint composition.</returns>
    public static RouteGroupBuilder WithNoStoreResponses(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.AddEndpointFilter(static async (context, next) =>
        {
            HttpCacheHeaders.SetNoStore(context.HttpContext);
            return await next(context).ConfigureAwait(false);
        });
    }
}
