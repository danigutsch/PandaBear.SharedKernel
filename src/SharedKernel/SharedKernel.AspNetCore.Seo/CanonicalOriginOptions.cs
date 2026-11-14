namespace PandaBear.AspNetCore.Seo;

/// <summary>
/// The canonical origin of a public site (scheme, host and port), bound from configuration by
/// <see cref="CanonicalOriginServiceCollectionExtensions.AddCanonicalOrigin"/>. Build canonical, <c>hreflang</c>, OpenGraph and sitemap
/// addresses from it, never from the request's <c>Host</c> header, so a forged header cannot end up in a cached page.
/// </summary>
public sealed class CanonicalOriginOptions
{
    /// <summary>Gets or sets the configured origin, such as <c>https://example.com</c>; it must pass <see cref="SitemapCanonicalOrigin.IsValid"/>.</summary>
    public string? Origin { get; set; }

    /// <summary>Gets the origin as an absolute URI, such as <c>https://example.com/</c>.</summary>
    /// <exception cref="InvalidOperationException"><see cref="Origin"/> is not a valid canonical origin.</exception>
    public Uri Value => SitemapCanonicalOrigin.IsValid(Origin)
        ? new Uri(Origin!, UriKind.Absolute)
        : throw new InvalidOperationException("The canonical origin is not an absolute http or https origin without a path.");

    /// <summary>Makes an absolute address on the canonical origin from a root-relative path.</summary>
    /// <param name="path">The path, starting with a single <c>/</c>, such as <c>/tours/patagonia</c>.</param>
    /// <returns>The absolute address.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank, not root-relative, or protocol-relative (<c>//host</c>).</exception>
    /// <exception cref="InvalidOperationException"><see cref="Origin"/> is not a valid canonical origin.</exception>
    public Uri Absolute(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("The path must be root-relative, such as /tours.", nameof(path));
        }

        return new Uri(Value, path);
    }
}
