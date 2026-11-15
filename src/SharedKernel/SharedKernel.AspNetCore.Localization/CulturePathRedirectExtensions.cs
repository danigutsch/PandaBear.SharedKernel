using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PandaBear.BuildingBlocks;

namespace PandaBear.AspNetCore.Localization;

/// <summary>
/// Adds the culture path redirect middleware and the remembered-choice cookie writer.
/// </summary>
public static class CulturePathRedirectExtensions
{
    /// <summary>
    /// The default name of the cookie that remembers the visitor's culture choice.
    /// </summary>
    public const string DefaultCookieName = "culture-choice";

    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>
    /// Redirects GET and HEAD requests that have no supported culture prefix (<c>/</c>, <c>/tours</c>, <c>/fr/tours</c>) to the
    /// same local path under the best culture with a 302. The culture is the remembered-choice cookie, then the
    /// Accept-Language header by quality, then <see cref="SupportedCultures.Default" />. The response varies on
    /// <c>Accept-Language</c> and <c>Cookie</c>. A first segment that names a supported culture in a non-canonical spelling
    /// (<c>/EN/tours</c>, <c>/%20en/tours</c>) is redirected with a 301 to the canonical segment (<c>/en/tours</c>), keeping the
    /// rest of the path and the query, so every page has a single URL. The target is always the path base, a supported culture,
    /// and the original local path and query, so the redirect cannot leave the site. Paths under an excluded prefix pass
    /// through. A path whose last segment contains a <c>.</c> (<c>/robots.txt</c>, <c>/en/site.css</c>) is treated as a file and
    /// passes through without localization; a <see cref="Slug" /> never contains <c>.</c>, so slug-based content paths are
    /// always localized. Register it after static files.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="cookieName">The name of the remembered-choice cookie.</param>
    /// <param name="excludedPrefixes">Path prefixes (such as <c>/api</c> or <c>/_blazor</c>) that are never redirected.</param>
    /// <returns>The application builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app" /> or <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cookieName" /> is empty or white-space.</exception>
    public static IApplicationBuilder UseCulturePathRedirect(
        this IApplicationBuilder app,
        SupportedCultures supportedCultures,
        string cookieName = DefaultCookieName,
        IReadOnlyCollection<PathString>? excludedPrefixes = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(supportedCultures);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);

        PathString[] excluded = [.. excludedPrefixes ?? []];
        return app.Use((context, next) => RedirectOrContinue(context, next, supportedCultures, cookieName, excluded));
    }

    /// <summary>
    /// Remembers the visitor's culture choice in a one-year, HTTP-only, SameSite=Lax cookie that
    /// <see cref="UseCulturePathRedirect" /> reads. Call it from a language-switch endpoint.
    /// </summary>
    /// <remarks>
    /// The cookie path is the request's path base (<c>/</c> when it is empty), so an application hosted under <c>/app</c> does
    /// not set a cookie for the whole host. <c>Secure</c> follows <see cref="HttpRequest.IsHttps" />: behind a proxy or load
    /// balancer that terminates TLS, configure forwarded headers (<c>UseForwardedHeaders</c> with <c>X-Forwarded-Proto</c>)
    /// before this runs, or the cookie is sent without <c>Secure</c>. The cookie is marked <c>IsEssential</c>: the language
    /// preference is treated as strictly necessary, so a cookie-consent policy does not suppress it; it holds only the culture
    /// name, never personal data.
    /// </remarks>
    /// <param name="response">The response that receives the cookie.</param>
    /// <param name="culture">The chosen culture. It must be one of the supported cultures.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="cookieName">The name of the cookie.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="culture" /> is not supported or <paramref name="cookieName" /> is blank.</exception>
    public static void RememberCulture(
        this HttpResponse response,
        ContentCulture culture,
        SupportedCultures supportedCultures,
        string cookieName = DefaultCookieName)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(supportedCultures);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);

        if (!supportedCultures.Contains(culture))
        {
            throw new ArgumentException("The culture is not one of the supported cultures.", nameof(culture));
        }

        response.Cookies.Append(
            cookieName,
            culture.Value,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = response.HttpContext.Request.IsHttps,
                MaxAge = CookieLifetime,
                // The browser matches the cookie path against the escaped request path.
                Path = response.HttpContext.Request.PathBase.HasValue ? response.HttpContext.Request.PathBase.ToUriComponent() : "/",
                IsEssential = true
            });
    }

    private static Task RedirectOrContinue(
        HttpContext context,
        RequestDelegate next,
        SupportedCultures supportedCultures,
        string cookieName,
        PathString[] excludedPrefixes)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
            || CulturePath.TryParse(request.Path, supportedCultures, out _, out _)
            || HasFileExtension(request.Path)
            || IsExcluded(request.Path, excludedPrefixes))
        {
            return next(context);
        }

        if (CulturePath.TryParseNonCanonical(request.Path, supportedCultures, out var named, out var remainder))
        {
            // Same page, other spelling of the culture segment: one permanent, preference-independent redirect to the
            // canonical URL. The target is built like the one below, so it cannot leave the site either.
            var canonical = request.PathBase + CulturePath.Prepend(remainder, named, supportedCultures) + request.QueryString;
            context.Response.Redirect(canonical, permanent: true);
            return Task.CompletedTask;
        }

        var culture = FromCookie(request, supportedCultures, cookieName)
            ?? FromAcceptLanguage(request, supportedCultures)
            ?? supportedCultures.Default;

        // The target is the path base plus a supported culture plus the original local path, so it always starts with
        // the site's own path base and a supported culture and can never be a scheme-relative or absolute URL.
        var location = request.PathBase + CulturePath.Prepend(request.Path, culture, supportedCultures) + request.QueryString;
        context.Response.Headers.Append(Microsoft.Net.Http.Headers.HeaderNames.Vary, "Accept-Language, Cookie");
        context.Response.Redirect(location, permanent: false);
        return Task.CompletedTask;
    }

    private static bool IsExcluded(PathString path, PathString[] excludedPrefixes)
    {
        foreach (var prefix in excludedPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFileExtension(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var lastSegment = value.AsSpan(value.LastIndexOf('/', StringComparison.Ordinal) + 1);
        return lastSegment.Contains('.');
    }

    private static ContentCulture? FromCookie(HttpRequest request, SupportedCultures supportedCultures, string cookieName) =>
        request.Cookies.TryGetValue(cookieName, out var value) ? supportedCultures.Resolve(value) : null;

    private static ContentCulture? FromAcceptLanguage(HttpRequest request, SupportedCultures supportedCultures)
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
