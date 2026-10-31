using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PandaBear.HttpCaching.AspNetCore;

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is <see langword="null"/>.</exception>
    public static RouteGroupBuilder WithNoStoreResponses(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.AddEndpointFilter(static async (context, next) =>
        {
            HttpCacheHeaders.SetNoStore(context.HttpContext);
            return await next(context).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Applies no-store headers to a route group and all requests beneath its absolute path prefix.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pathPrefix"/> has no value.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Use <see cref="ApplicationBuilderHttpCacheExtensions.UseNoStoreResponses" /> before authentication
    /// and other response-producing middleware to protect authorization, binding, rate-limit, and unmatched responses.
    /// </remarks>
    /// <param name="group">The route group to protect.</param>
    /// <param name="pathPrefix">The absolute literal group prefix, including any parent group prefixes.</param>
    /// <returns>The same route group for fluent endpoint composition.</returns>
    public static RouteGroupBuilder WithNoStoreResponses(this RouteGroupBuilder group, PathString pathPrefix)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!pathPrefix.HasValue)
        {
            throw new ArgumentException("The no-store group prefix must be an absolute path.", nameof(pathPrefix));
        }

        return group.WithMetadata(new NoStoreResponseScope(pathPrefix)).WithNoStoreResponses();
    }

}
