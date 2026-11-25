using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using SharedKernel.BuildingBlocks;

namespace SharedKernel.AspNetCore.Localization;

/// <summary>
/// Determines the request culture from the first path segment (<c>/pt/...</c>, <c>/en/...</c>) against a <see cref="SupportedCultures" /> set.
/// The request culture is the segment itself, or, when created with <see cref="CultureLanguageTags" />, the language tag its culture
/// declares (<c>/pt/...</c> gives <c>pt-BR</c>) while the address keeps the segment.
/// </summary>
public sealed class PathCultureRequestCultureProvider : RequestCultureProvider
{
    private readonly SupportedCultures _supportedCultures;
    private readonly CultureLanguageTags? _languageTags;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathCultureRequestCultureProvider" /> class whose request culture is the path segment.
    /// </summary>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    public PathCultureRequestCultureProvider(SupportedCultures supportedCultures)
    {
        ArgumentNullException.ThrowIfNull(supportedCultures);

        _supportedCultures = supportedCultures;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathCultureRequestCultureProvider" /> class whose request culture is the language tag
    /// that the culture of the path segment declares. Register the same tags as the supported cultures and UI cultures of the request
    /// localization options, or the middleware falls back to a parent such as <c>pt</c>.
    /// </summary>
    /// <param name="languageTags">The supported cultures and the language tag each one declares.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="languageTags" /> is <see langword="null" />.</exception>
    public PathCultureRequestCultureProvider(CultureLanguageTags languageTags)
    {
        ArgumentNullException.ThrowIfNull(languageTags);

        _supportedCultures = languageTags.Cultures;
        _languageTags = languageTags;
    }

    /// <inheritdoc />
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ProviderCultureResult? result = CulturePath.TryParse(httpContext.Request.Path, _supportedCultures, out var culture, out _)
            ? new ProviderCultureResult((_languageTags?.LanguageTagOf(culture) ?? culture).Value)
            : null;
        return Task.FromResult(result);
    }
}
