using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace SharedKernel.HttpCaching.AspNetCore;

/// <summary>
/// Provides ASP.NET Core response-header helpers for standard HTTP cache directives.
/// </summary>
public static class HttpCacheHeaders
{
    private const string StaleWhileRevalidateDirective = "stale-while-revalidate";

    private const string PragmaNoCache = "no-cache";

    private const string ExpiredAtUnixEpochHttpDate = "Thu, 01 Jan 1970 00:00:00 GMT";

    /// <summary>
    /// Sets a public <c>Cache-Control</c> header with the provided freshness lifetime.
    /// </summary>
    /// <param name="httpContext">The request context whose response headers are updated.</param>
    /// <param name="maxAge">The public cache freshness lifetime.</param>
    /// <param name="staleWhileRevalidate">The optional stale-while-revalidate lifetime.</param>
    public static void SetPublic(HttpContext httpContext, TimeSpan maxAge, TimeSpan? staleWhileRevalidate = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var cacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = maxAge
        };
        if (staleWhileRevalidate is not null)
        {
            var staleSeconds = staleWhileRevalidate.Value.TotalSeconds.ToString("0", CultureInfo.InvariantCulture);
            cacheControl.Extensions.Add(new NameValueHeaderValue(StaleWhileRevalidateDirective, staleSeconds));
        }

        httpContext.Response.GetTypedHeaders().CacheControl = cacheControl;
    }

    /// <summary>
    /// Sets response headers that prevent storage by browsers and intermediaries.
    /// </summary>
    /// <param name="httpContext">The request context whose response headers are updated.</param>
    public static void SetNoStore(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            NoStore = true
        };
        httpContext.Response.Headers[HeaderNames.Pragma] = PragmaNoCache;
        httpContext.Response.Headers[HeaderNames.Expires] = ExpiredAtUnixEpochHttpDate;
    }
}
