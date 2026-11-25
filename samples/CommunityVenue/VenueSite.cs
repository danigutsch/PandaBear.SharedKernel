using SharedKernel.AspNetCore.Seo;
using SharedKernel.Blazor.Site;
using SharedKernel.BuildingBlocks;

namespace CommunityVenue;

/// <summary>The venue's immutable content and appearance snapshot.</summary>
internal sealed class VenueSite(IReadOnlyList<VenuePage> pages, Uri origin, string imageName, string themeVersion)
{
    /// <summary>Gets the immutable published image address.</summary>
    internal string ImageAddress => "/media/" + imageName;

    /// <summary>Gets the theme stylesheet address for this snapshot.</summary>
    internal string ThemeAddress => "/theme.css?v=" + themeVersion;

    /// <summary>Finds a published page at the given local path.</summary>
    internal VenuePage? Find(string path) =>
        pages.FirstOrDefault(page => string.Equals(page.Path, path, StringComparison.Ordinal));

    /// <summary>Gets the requested translation's path or its default redirect when it is unavailable.</summary>
    internal string PathFor(string key, string language) =>
        pages.FirstOrDefault(page => page.Key == key && page.Language == language)?.Path ?? "/" + key;

    /// <summary>Gets the redirect destination of one published page group.</summary>
    internal string DefaultPathFor(string key) =>
        pages.FirstOrDefault(page => page.Key == key && page.Language == "en")?.Path
        ?? pages.First(page => page.Key == key).Path;

    /// <summary>Gets the local navigation items for one language.</summary>
    internal IReadOnlyList<SiteNavItem> Navigation(string language) => [.. pages
        .Where(page => page.Language == language)
        .Select(page => new SiteNavItem(page.Title, LinkTarget.FromString(page.Path)))];

    /// <summary>Gets links to the published translations of a page.</summary>
    internal IReadOnlyList<LanguageLink> Languages(VenuePage current) => [.. pages
        .Where(page => page.Key == current.Key)
        .Select(page => new LanguageLink(ContentCulture.Create(page.Language).Value,
            page.Language == "pt" ? "Português" : "English", LinkTarget.FromString(page.Path), page.Language == current.Language))];

    /// <summary>Gets the canonical address of a local path.</summary>
    internal Uri Absolute(string path) => new(origin, path);

    /// <summary>Gets all published alternate-language addresses of a page.</summary>
    internal IReadOnlyList<AlternateLink> Alternates(VenuePage current) => [.. pages
        .Where(page => page.Key == current.Key)
        .Select(page => new AlternateLink(ContentCulture.Create(page.Language).Value, Absolute(page.Path)))];

    /// <summary>Builds the public sitemap from the published translations.</summary>
    internal IEnumerable<SitemapEntry> Sitemap() => pages.GroupBy(page => page.Key).SelectMany(group =>
        LocalizedSitemap.Build(group.Select(page => (new SitemapAlternate(page.Language, Absolute(page.Path)), (DateTimeOffset?)page.Modified)),
            Absolute("/" + group.Key)));
}
