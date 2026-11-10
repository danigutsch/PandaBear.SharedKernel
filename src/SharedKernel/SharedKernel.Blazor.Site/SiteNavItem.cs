namespace PandaBear.Blazor.Site;

/// <summary>An entry of the header navigation: a link, or a group of links shown as a native disclosure.</summary>
/// <param name="Text">The visible text.</param>
/// <param name="Href">The link target. Set exactly one of <paramref name="Href" /> and <paramref name="Children" />.</param>
/// <param name="Children">The links of a group, one level only.</param>
public sealed record SiteNavItem(string Text, LinkTarget? Href = null, IReadOnlyList<SiteNavItem>? Children = null);
