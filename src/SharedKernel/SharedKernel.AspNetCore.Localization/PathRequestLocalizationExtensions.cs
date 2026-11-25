using Microsoft.AspNetCore.Builder;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.AspNetCore.Localization;

/// <summary>Request localization that takes the language of a request from the culture segment of its path only.</summary>
public static class PathRequestLocalizationExtensions
{
    /// <summary>
    /// Adds request localization whose only source is the first path segment (<see cref="PathCultureRequestCultureProvider" />): the
    /// request's culture and UI culture are the language tag the segment's culture declares (<c>/pt/...</c> gives <c>pt-BR</c>), or the
    /// default culture's tag for any other path. The response names that language in <c>Content-Language</c>. Neither
    /// <c>Accept-Language</c>, a query string nor a cookie changes it, so a page's address alone decides its language.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="languageTags">The supported cultures and the language tag each declares.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app" /> or <paramref name="languageTags" /> is <see langword="null" />.</exception>
    public static IApplicationBuilder UsePathRequestLocalization(this IApplicationBuilder app, CultureLanguageTags languageTags)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(languageTags);
        return Use(app, languageTags.DefaultLanguageTag, languageTags.LanguageTags, new PathCultureRequestCultureProvider(languageTags));
    }

    /// <summary>
    /// Adds request localization whose only source is the first path segment (<see cref="PathCultureRequestCultureProvider" />): the
    /// request's culture and UI culture are the segment's culture (<c>/en/...</c> gives <c>en</c>), or the default culture for any other
    /// path. The response names that language in <c>Content-Language</c>. Neither <c>Accept-Language</c>, a query string nor a cookie
    /// changes it, so a page's address alone decides its language.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <returns>The same application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app" /> or <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    public static IApplicationBuilder UsePathRequestLocalization(this IApplicationBuilder app, SupportedCultures supportedCultures)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(supportedCultures);
        return Use(app, supportedCultures.Default, supportedCultures.Cultures, new PathCultureRequestCultureProvider(supportedCultures));
    }

    private static IApplicationBuilder Use(
        IApplicationBuilder app,
        ContentCulture defaultCulture,
        IEnumerable<ContentCulture> cultures,
        PathCultureRequestCultureProvider provider)
    {
        string[] names = [.. cultures.Select(static culture => culture.Value)];
        return app.UseRequestLocalization(options =>
        {
            options.SetDefaultCulture(defaultCulture.Value).AddSupportedCultures(names).AddSupportedUICultures(names);
            options.ApplyCurrentCultureToResponseHeaders = true;
            options.RequestCultureProviders = [provider];
        });
    }
}
