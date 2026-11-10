namespace PandaBear.AspNetCore.Seo;

/// <summary>
/// Represents one URL entry in a sitemap XML document.
/// </summary>
public sealed class SitemapEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapEntry" /> class.
    /// </summary>
    /// <param name="location">The absolute HTTP or HTTPS URL for the sitemap entry.</param>
    /// <param name="lastModified">The optional last modification timestamp.</param>
    /// <param name="alternates">
    /// The optional alternate-language URLs of the entry, written as <c>xhtml:link</c> elements. Per the hreflang convention the
    /// list should include the entry's own language and may include <see cref="SitemapAlternate.DefaultLanguage" />.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="location" /> or a member of <paramref name="alternates" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="location" /> is not an absolute HTTP or HTTPS URL.</exception>
    public SitemapEntry(Uri location, DateTimeOffset? lastModified = null, IEnumerable<SitemapAlternate>? alternates = null)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!location.IsAbsoluteUri
            || (!string.Equals(location.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(location.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Sitemap entry locations must be absolute HTTP or HTTPS URLs.",
                nameof(location));
        }

        var copy = new List<SitemapAlternate>();
        foreach (var alternate in alternates ?? [])
        {
            ArgumentNullException.ThrowIfNull(alternate);
            copy.Add(alternate);
        }

        Location = location;
        LastModified = lastModified;
        Alternates = copy;
    }

    /// <summary>
    /// Gets the absolute URL for the sitemap entry.
    /// </summary>
    public Uri Location
    {
        get;
    }

    /// <summary>
    /// Gets the optional last modification timestamp.
    /// </summary>
    public DateTimeOffset? LastModified
    {
        get;
    }

    /// <summary>
    /// Gets the alternate-language URLs of the entry; empty when none were given.
    /// </summary>
    public IReadOnlyList<SitemapAlternate> Alternates
    {
        get;
    }
}
