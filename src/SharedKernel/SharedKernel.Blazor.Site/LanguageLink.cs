using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>One language of a <see cref="LanguageSwitcher" />.</summary>
/// <param name="Culture">The culture of the linked page, for example <c>pt-BR</c>.</param>
/// <param name="Label">The visible name of the language, written in that language.</param>
/// <param name="Href">The address of the page in that language.</param>
/// <param name="IsCurrent">Whether this is the language of the current page.</param>
public sealed record LanguageLink(ContentCulture Culture, string Label, LinkTarget Href, bool IsCurrent = false);
