using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PandaBear.HttpCaching.AspNetCore;

/// <summary>Serves caller-owned versioned stylesheets.</summary>
public static class VersionedStylesheetExtensions
{
    /// <summary>Maps GET and HEAD, applying immutable caching only to a single exact <c>v</c> query value.</summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <param name="pattern">The stylesheet route.</param>
    /// <param name="snapshot">Loads the current CSS and its version from the same snapshot.</param>
    /// <returns>The mapped endpoint.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static IEndpointConventionBuilder MapVersionedStylesheet(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, ValueTask<(string Css, string Version)>> snapshot)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(snapshot);

        return endpoints.MapMethods(pattern, [HttpMethods.Get, HttpMethods.Head], context => Serve(context, snapshot));
    }

    private static async Task Serve(HttpContext context, Func<HttpContext, ValueTask<(string Css, string Version)>> snapshot)
    {
        var (css, version) = await snapshot(context).ConfigureAwait(false);
        var requested = context.Request.Query["v"];
        if (requested.Count == 1 && version.Length > 0 && string.Equals(requested[0], version, StringComparison.Ordinal))
        {
            HttpCacheHeaders.SetImmutable(context);
        }

        context.Response.ContentType = "text/css; charset=utf-8";
        context.Response.ContentLength = Encoding.UTF8.GetByteCount(css);
        if (HttpMethods.IsGet(context.Request.Method))
        {
            await context.Response.WriteAsync(css, context.RequestAborted).ConfigureAwait(false);
        }
    }
}
