using Microsoft.AspNetCore.Components;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The search and sharing metadata of a page, rendered into the host's <c>HeadOutlet</c>: title, description, canonical link,
/// OpenGraph basics and <c>hreflang</c> alternates including <c>x-default</c>. Addresses are absolute http or https <see cref="Uri" />
/// values. A component cannot set <c>&lt;html lang&gt;</c>; the host sets it from the request culture.
/// </summary>
public sealed partial class PageHead
{
    /// <summary>Gets or sets the page title, also used for <c>og:title</c>.</summary>
    [Parameter]
    [EditorRequired]
    public string Title
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the meta description, also used for <c>og:description</c>.</summary>
    [Parameter]
    public string? Description
    {
        get; set;
    }

    /// <summary>Gets or sets the canonical address, also used for <c>og:url</c>.</summary>
    [Parameter]
    public Uri? CanonicalUrl
    {
        get; set;
    }

    /// <summary>Gets or sets the site name for <c>og:site_name</c>.</summary>
    [Parameter]
    public string? SiteName
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the culture of the page, written to <c>og:locale</c> as language and region joined by an underscore: <c>pt-BR</c>
    /// gives <c>pt_BR</c>, and a script subtag is dropped, so <c>zh-Hant-TW</c> gives <c>zh_TW</c>.
    /// </summary>
    [Parameter]
    public ContentCulture? Locale
    {
        get; set;
    }

    /// <summary>Gets or sets the sharing image for <c>og:image</c>.</summary>
    [Parameter]
    public Uri? ImageUrl
    {
        get; set;
    }

    /// <summary>Gets or sets the language versions of the page, usually including the current one.</summary>
    [Parameter]
    public IReadOnlyList<AlternateLink> Alternates
    {
        get; set;
    } = [];

    /// <summary>Gets or sets the address for <c>hreflang="x-default"</c>, the version for languages not listed.</summary>
    [Parameter]
    public Uri? DefaultAlternateUrl
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentNullException.ThrowIfNull(Alternates);
        RequireWebAddress(CanonicalUrl, nameof(CanonicalUrl));
        RequireWebAddress(ImageUrl, nameof(ImageUrl));
        RequireWebAddress(DefaultAlternateUrl, nameof(DefaultAlternateUrl));
        foreach (var alternate in Alternates)
        {
            ArgumentNullException.ThrowIfNull(alternate, nameof(Alternates));
            ArgumentNullException.ThrowIfNull(alternate.Culture, nameof(Alternates));
            ArgumentNullException.ThrowIfNull(alternate.Href, nameof(Alternates));
            RequireWebAddress(alternate.Href, nameof(Alternates));
        }
    }

    private static string OpenGraphLocale(ContentCulture culture)
    {
        var subtags = culture.Value.Split('-');
        var region = subtags.Skip(1).FirstOrDefault(subtag => subtag.Length == 2 || (subtag.Length == 3 && subtag.All(char.IsAsciiDigit)));
        return region is null ? subtags[0] : subtags[0] + "_" + region;
    }

    private static void RequireWebAddress(Uri? address, string parameterName)
    {
        if (address is not null && (!address.IsAbsoluteUri || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("An absolute http or https address is required.", parameterName);
        }
    }
}
