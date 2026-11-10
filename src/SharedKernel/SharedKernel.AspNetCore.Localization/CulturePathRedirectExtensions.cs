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
    /// <c>Accept-Language</c> and <c>Cookie</c>. The target is always the path base, a supported culture, and the original
    /// local path and query, so the redirect cannot leave the site. Paths whose last segment has a file extension
    /// (<c>/robots.txt</c>) and paths under an excluded prefix pass through. Register it after static files.
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

        return app.UseMiddleware<CulturePathRedirectMiddleware>(supportedCultures, cookieName, excludedPrefixes ?? []);
    }

    /// <summary>
    /// Remembers the visitor's culture choice in a one-year, HTTP-only, SameSite=Lax cookie that
    /// <see cref="UseCulturePathRedirect" /> reads. Call it from a language-switch endpoint.
    /// </summary>
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
                Path = "/",
                IsEssential = true
            });
    }
}
