namespace SharedKernel.AspNetCore;

/// <summary>
/// Validates canonical public origins used in sitemap and robots.txt URLs.
/// </summary>
public static class SitemapCanonicalOrigin
{
    /// <summary>
    /// Determines whether the value is an absolute HTTP or HTTPS origin.
    /// </summary>
    /// <param name="value">The candidate origin.</param>
    /// <returns><see langword="true" /> when the value is a valid origin; otherwise <see langword="false" />.</returns>
    public static bool IsValid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Trim().Length == value.Length
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.IsWellFormedOriginalString()
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrEmpty(uri.Host)
            && uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && string.IsNullOrEmpty(uri.UserInfo)
            && !value.Contains('?', StringComparison.Ordinal)
            && !value.Contains('#', StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses a validated canonical origin.
    /// </summary>
    /// <param name="value">The canonical origin value.</param>
    /// <returns>The parsed absolute origin URI.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is not a valid origin.</exception>
    public static Uri Parse(string value)
    {
        return IsValid(value)
            ? new Uri(value, UriKind.Absolute)
            : throw new ArgumentException(
                "The sitemap canonical origin must be an absolute HTTP or HTTPS origin without a path, query, fragment, or userinfo.",
                nameof(value));
    }
}
