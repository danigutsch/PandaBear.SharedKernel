using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using PandaBear.BuildingBlocks;

namespace PandaBear.AspNetCore.Localization;

/// <summary>
/// Reads, adds, and swaps the leading culture segment of a request path such as <c>/pt/tours</c>.
/// Only cultures of a <see cref="SupportedCultures" /> set are ever read or written, so a built path cannot leave the site.
/// </summary>
public static class CulturePath
{
    /// <summary>
    /// Attempts to read a supported culture from the first path segment. Only the exact canonical name matches (ordinal:
    /// <c>/en/tours</c>, not <c>/EN/tours</c> or <c>/%20en/tours</c>), so each page has a single URL;
    /// <see cref="CulturePathRedirectExtensions.UseCulturePathRedirect" /> redirects the other spellings to it.
    /// </summary>
    /// <param name="path">The request path, relative to the path base.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="culture">The supported culture when found; otherwise <see langword="null" />.</param>
    /// <param name="remainder">The path after the culture segment (empty or starting with <c>/</c>) when found; otherwise the default path string.</param>
    /// <returns><see langword="true" /> when the first segment is the canonical name of a supported culture; otherwise <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    public static bool TryParse(
        PathString path,
        SupportedCultures supportedCultures,
        [NotNullWhen(true)] out ContentCulture? culture,
        out PathString remainder)
    {
        ArgumentNullException.ThrowIfNull(supportedCultures);

        culture = null;
        remainder = default;
        if (!TrySplit(path, out var segment, out var rest))
        {
            return false;
        }

        foreach (var supported in supportedCultures.Cultures)
        {
            if (string.Equals(segment, supported.Value, StringComparison.Ordinal))
            {
                culture = supported;
                remainder = rest;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds the culture segment in front of a path.
    /// </summary>
    /// <param name="path">The path without a culture segment.</param>
    /// <param name="culture">The culture to add. It must be one of the supported cultures.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <returns>The path starting with <c>/{culture}</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture" /> or <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="culture" /> is not supported.</exception>
    public static PathString Prepend(PathString path, ContentCulture culture, SupportedCultures supportedCultures)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(supportedCultures);

        if (!supportedCultures.Contains(culture))
        {
            throw new ArgumentException("The culture is not one of the supported cultures.", nameof(culture));
        }

        return new PathString("/" + culture.Value).Add(path);
    }

    /// <summary>
    /// Replaces the culture segment of a path, or adds one when the path has none. Use it to build language-switcher links.
    /// </summary>
    /// <param name="path">The current path, with or without a supported culture segment.</param>
    /// <param name="culture">The target culture. It must be one of the supported cultures.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <returns>The same local path under the target culture.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture" /> or <paramref name="supportedCultures" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="culture" /> is not supported.</exception>
    public static PathString Switch(PathString path, ContentCulture culture, SupportedCultures supportedCultures)
    {
        ArgumentNullException.ThrowIfNull(supportedCultures);

        return Prepend(TryParse(path, supportedCultures, out _, out var remainder) ? remainder : path, culture, supportedCultures);
    }

    /// <summary>
    /// Reads a first segment that names a supported culture only in another spelling (<c>/EN/tours</c>, <c>/%20en/tours</c>),
    /// so the caller can redirect it to the canonical segment.
    /// </summary>
    /// <param name="path">The request path, relative to the path base.</param>
    /// <param name="supportedCultures">The supported cultures.</param>
    /// <param name="culture">The supported culture the segment names when found; otherwise <see langword="null" />.</param>
    /// <param name="remainder">The path after the segment (empty or starting with <c>/</c>) when found; otherwise the default path string.</param>
    /// <returns><see langword="true" /> when the segment names a supported culture but is not its canonical name.</returns>
    internal static bool TryParseNonCanonical(
        PathString path,
        SupportedCultures supportedCultures,
        [NotNullWhen(true)] out ContentCulture? culture,
        out PathString remainder)
    {
        culture = null;
        remainder = default;
        if (!TrySplit(path, out var segment, out var rest)
            || !supportedCultures.TryGet(segment, out var named)
            || string.Equals(segment, named.Value, StringComparison.Ordinal))
        {
            return false;
        }

        culture = named;
        remainder = rest;
        return true;
    }

    private static bool TrySplit(PathString path, out string segment, out PathString remainder)
    {
        segment = string.Empty;
        remainder = default;

        var value = path.Value;
        if (string.IsNullOrEmpty(value) || value[0] != '/')
        {
            return false;
        }

        var end = value.IndexOf('/', 1);
        segment = end < 0 ? value[1..] : value[1..end];
        remainder = end < 0 ? PathString.Empty : new PathString(value[end..]);
        return true;
    }
}
