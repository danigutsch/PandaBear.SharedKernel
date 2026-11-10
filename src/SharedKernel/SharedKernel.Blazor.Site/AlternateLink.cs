namespace PandaBear.Blazor.Site;

/// <summary>An alternate-language version of a page, rendered as a <c>hreflang</c> link.</summary>
/// <param name="Culture">The language tag, for example <c>pt-BR</c>.</param>
/// <param name="Href">The absolute http or https address of the page in that language.</param>
public sealed record AlternateLink(string Culture, Uri Href);
