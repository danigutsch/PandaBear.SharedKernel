using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using PandaBear.BuildingBlocks;

namespace PandaBear.AspNetCore.Localization;

/// <summary>
/// Determines the request culture from the first path segment (<c>/pt/...</c>, <c>/en/...</c>) against a <see cref="SupportedCultures" /> set.
/// </summary>
public sealed class PathCultureRequestCultureProvider : RequestCultureProvider
{
    private readonly SupportedCultures _supportedCultures;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathCultureRequestCultureProvider" /> class.
    /// </summary>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    public PathCultureRequestCultureProvider(SupportedCultures supportedCultures)
    {
        ArgumentNullException.ThrowIfNull(supportedCultures);

        _supportedCultures = supportedCultures;
    }

    /// <inheritdoc />
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ProviderCultureResult? result = CulturePath.TryParse(httpContext.Request.Path, _supportedCultures, out var culture, out _)
            ? new ProviderCultureResult(culture.Value)
            : null;
        return Task.FromResult(result);
    }
}
