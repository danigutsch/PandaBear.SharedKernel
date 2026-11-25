using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace SharedKernel.HttpCaching.AspNetCore;

/// <summary>
/// Provides ASP.NET Core response-header helpers for standard HTTP cache directives.
/// </summary>
public static class HttpCacheHeaders
{
    /// <summary>
    /// The <c>Cache-Control</c> value of a response whose URL addresses its content: public, one year (the practical maximum), and
    /// <c>immutable</c>, so browsers do not revalidate it on reload.
    /// </summary>
    public const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private const string StaleWhileRevalidateDirective = "stale-while-revalidate";

    private const string PragmaNoCache = "no-cache";

    private const string ExpiredAtUnixEpochHttpDate = "Thu, 01 Jan 1970 00:00:00 GMT";

    /// <summary>
    /// Sets a public <c>Cache-Control</c> header with the provided freshness lifetime.
    /// </summary>
    /// <param name="httpContext">The request context whose response headers are updated.</param>
    /// <param name="maxAge">The public cache freshness lifetime.</param>
    /// <param name="staleWhileRevalidate">The optional stale-while-revalidate lifetime.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpContext"/> is <see langword="null"/>.</exception>
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
    /// Sets <see cref="ImmutableCacheControl"/> on a response whose bytes never change under its URL, such as a content-hashed file, and
    /// removes the <c>Pragma</c> and <c>Expires</c> headers that <see cref="SetNoStore"/> sets. Use it only when a new content gets a new URL.
    /// It sets no other header: serve such files with <c>X-Content-Type-Options: nosniff</c> as any other upload.
    /// </summary>
    /// <param name="httpContext">The request context whose response headers are updated.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpContext"/> is <see langword="null"/>.</exception>
    public static void SetImmutable(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var headers = httpContext.Response.Headers;
        headers.CacheControl = ImmutableCacheControl;
        headers.Remove(HeaderNames.Pragma);
        headers.Remove(HeaderNames.Expires);
    }

    /// <summary>
    /// Sets response headers that prevent storage by browsers and intermediaries.
    /// </summary>
    /// <param name="httpContext">The request context whose response headers are updated.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpContext"/> is <see langword="null"/>.</exception>
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
