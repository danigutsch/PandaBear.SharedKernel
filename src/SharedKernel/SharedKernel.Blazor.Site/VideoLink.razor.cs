using Microsoft.AspNetCore.Components;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A link to a video on the site that hosts it, instead of an embedded player: a self-hosted poster with a play mark, the title and a
/// visually hidden hint that the link opens the video's site. The page loads nothing from that site and needs no frame or script, so
/// a <c>frame-src</c>-free content security policy holds and the visitor's first request to the video site is their click.
/// </summary>
public sealed partial class VideoLink
{
    /// <summary>Gets or sets the address of the video, an absolute <c>https</c> address.</summary>
    [Parameter]
    [EditorRequired]
    public LinkTarget? Href { get; set; }

    /// <summary>Gets or sets the title of the video, shown under the poster and the start of the link's name.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hint read after the title that says where the link goes, for example "(opens YouTube)". Caller-supplied so it
    /// can be localized.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string SiteHint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the poster, loaded lazily. It must be decorative, because the title names the link, and self-hosted
    /// (root-relative, every candidate too), so showing it requests nothing from the video's site.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public ResponsiveImage? Poster { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Href);
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(SiteHint);
        ArgumentNullException.ThrowIfNull(Poster);
        if (!WebUrl.CreateHttps(Href.Value).IsSuccess)
        {
            throw new ArgumentException("A video address must be an absolute https address.", nameof(Href));
        }

        if (!Poster.Alt.IsDecorative)
        {
            throw new ArgumentException("The poster must be decorative, because the title names the link.", nameof(Poster));
        }

        var addresses = Poster.SrcSet.Concat(Poster.Sources.SelectMany(source => source.Candidates)).Select(candidate => candidate.Src).Prepend(Poster.Src);
        if (addresses.Any(address => !address.Value.StartsWith('/', StringComparison.Ordinal)))
        {
            throw new ArgumentException("The poster must be self-hosted at root-relative addresses, so the page requests nothing from the video's site.", nameof(Poster));
        }
    }
}
