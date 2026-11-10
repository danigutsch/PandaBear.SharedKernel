using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using PandaBear.BuildingBlocks;

namespace PandaBear.AspNetCore.Localization;

/// <summary>
/// Redirects GET and HEAD requests without a supported culture path prefix to the same local path under the best culture.
/// </summary>
internal sealed class CulturePathRedirectMiddleware(
    RequestDelegate next,
    SupportedCultures supportedCultures,
    string cookieName,
    IReadOnlyCollection<PathString> excludedPrefixes)
{
    /// <summary>
    /// Redirects the request when it needs a culture prefix; otherwise runs the next middleware.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public Task Invoke(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
            || CulturePath.TryParse(request.Path, supportedCultures, out _, out _)
            || HasFileExtension(request.Path)
            || excludedPrefixes.Any(prefix => request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return next(context);
        }

        var culture = FromCookie(request) ?? FromAcceptLanguage(request) ?? supportedCultures.Default;

        // The target is the path base plus a supported culture plus the original local path, so it always starts with
        // the site's own path base and a supported culture and can never be a scheme-relative or absolute URL.
        var location = request.PathBase + CulturePath.Prepend(request.Path, culture, supportedCultures) + request.QueryString;
        context.Response.Headers.Append(HeaderNames.Vary, "Accept-Language, Cookie");
        context.Response.Redirect(location, permanent: false);
        return Task.CompletedTask;
    }

    private static bool HasFileExtension(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var lastSegment = value.AsSpan(value.LastIndexOf('/') + 1);
        return lastSegment.Contains('.');
    }

    private ContentCulture? FromCookie(HttpRequest request)
    {
        return request.Cookies.TryGetValue(cookieName, out var value) ? supportedCultures.Resolve(value) : null;
    }

    private ContentCulture? FromAcceptLanguage(HttpRequest request)
    {
        foreach (var language in request.GetTypedHeaders().AcceptLanguage.OrderByDescending(static l => l.Quality ?? 1.0))
        {
            if (language.Quality is 0 || !language.Value.HasValue)
            {
                continue;
            }

            if (supportedCultures.Resolve(language.Value.Value) is { } culture)
            {
                return culture;
            }
        }

        return null;
    }
}
