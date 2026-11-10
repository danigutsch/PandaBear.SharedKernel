namespace PandaBear.AspNetCore.Seo;

/// <summary>
/// Represents one alternate-language URL of a sitemap entry, written as an <c>xhtml:link rel="alternate" hreflang</c> element.
/// </summary>
public sealed class SitemapAlternate
{
    /// <summary>
    /// The <c>hreflang</c> value that marks the fallback URL for users whose language is not listed.
    /// </summary>
    public const string DefaultLanguage = "x-default";

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapAlternate" /> class.
    /// </summary>
    /// <param name="language">The <c>hreflang</c> value: a language tag such as <c>pt</c> or <c>pt-BR</c>, or <see cref="DefaultLanguage" />.</param>
    /// <param name="location">The absolute HTTP or HTTPS URL of the alternate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="language" /> or <paramref name="location" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="language" /> is not a language tag or <paramref name="location" /> is not an absolute HTTP or HTTPS URL.
    /// </exception>
    public SitemapAlternate(string language, Uri location)
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(location);

        if (!IsLanguageTag(language))
        {
            throw new ArgumentException("The alternate language must be a language tag such as 'pt-BR' or 'x-default'.", nameof(language));
        }

        if (!location.IsAbsoluteUri
            || (!string.Equals(location.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(location.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Sitemap alternate locations must be absolute HTTP or HTTPS URLs.", nameof(location));
        }

        Language = language;
        Location = location;
    }

    /// <summary>
    /// Gets the <c>hreflang</c> value.
    /// </summary>
    public string Language
    {
        get;
    }

    /// <summary>
    /// Gets the absolute URL of the alternate.
    /// </summary>
    public Uri Location
    {
        get;
    }

    /// <summary>
    /// Creates the <see cref="DefaultLanguage" /> alternate for a fallback URL.
    /// </summary>
    /// <param name="location">The absolute HTTP or HTTPS URL of the fallback.</param>
    /// <returns>The <c>x-default</c> alternate.</returns>
    public static SitemapAlternate ForDefault(Uri location)
    {
        return new SitemapAlternate(DefaultLanguage, location);
    }

    private static bool IsLanguageTag(string value)
    {
        var partLength = 0;
        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                partLength++;
            }
            else if (character == '-' && partLength > 0)
            {
                partLength = 0;
            }
            else
            {
                return false;
            }
        }

        return partLength > 0;
    }
}
