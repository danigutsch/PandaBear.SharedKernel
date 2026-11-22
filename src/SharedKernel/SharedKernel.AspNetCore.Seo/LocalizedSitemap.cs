namespace PandaBear.AspNetCore.Seo;

/// <summary>Builds the reciprocal sitemap entries of one published page.</summary>
public static class LocalizedSitemap
{
    /// <summary>Builds entries in caller order, each with all published language links and an optional fallback.</summary>
    /// <param name="translations">Published language URLs and their individual timestamps, in display order.</param>
    /// <param name="fallback">The optional absolute HTTP or HTTPS address for x-default.</param>
    /// <returns>One entry per translation, including its own alternate link.</returns>
    /// <exception cref="ArgumentNullException">The translations or one of their links is null.</exception>
    /// <exception cref="ArgumentException">A language is duplicated or reserved, or the fallback URL is invalid.</exception>
    public static IReadOnlyList<SitemapEntry> Build(
        IEnumerable<(SitemapAlternate Link, DateTimeOffset? LastModified)> translations,
        Uri? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(translations);
        var published = translations.ToArray();
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SitemapAlternate.DefaultLanguage };
        var alternates = new List<SitemapAlternate>(published.Length + 1);
        foreach (var (link, _) in published)
        {
            ArgumentNullException.ThrowIfNull(link);
            if (!languages.Add(link.Language))
            {
                throw new ArgumentException("Each translation must have a distinct language other than x-default.", nameof(translations));
            }

            alternates.Add(link);
        }

        if (fallback is not null)
        {
            alternates.Add(SitemapAlternate.ForDefault(fallback));
        }

        return [.. published.Select(item => new SitemapEntry(item.Link.Location, item.LastModified, alternates))];
    }
}
