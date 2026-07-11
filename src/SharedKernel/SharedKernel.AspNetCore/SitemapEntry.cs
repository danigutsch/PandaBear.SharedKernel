namespace SharedKernel.AspNetCore;

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
    /// <exception cref="ArgumentException">Thrown when <paramref name="location" /> is not an absolute HTTP or HTTPS URL.</exception>
    public SitemapEntry(Uri location, DateTimeOffset? lastModified = null)
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

        Location = location;
        LastModified = lastModified;
    }

    /// <summary>
    /// Gets the absolute URL for the sitemap entry.
    /// </summary>
    public Uri Location { get; }

    /// <summary>
    /// Gets the optional last modification timestamp.
    /// </summary>
    public DateTimeOffset? LastModified { get; }
}
