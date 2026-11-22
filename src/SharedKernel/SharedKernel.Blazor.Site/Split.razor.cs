using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// Text beside media, such as photos, without a heading of its own: a plain <c>div</c>, never a landmark, with the text in
/// <c>site-split__main</c> and the media in <c>site-split__media</c>, in reading order. From the <c>48rem</c> breakpoint on they are two
/// equal columns, the media on the <see cref="MediaSide" />, both starting at the top unless <see cref="Alignment" /> centres them; below
/// it the media follows the text. <see cref="Section.Media" /> uses it
/// for a section's split.
/// </summary>
public sealed partial class Split
{
    /// <summary>Gets or sets the text.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the media beside the text, which fills its column.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? Media { get; set; }

    /// <summary>
    /// Gets or sets the side of the media column from the breakpoint on. The default is <see cref="SplitMediaSide.End" />;
    /// <see cref="SplitMediaSide.Start" /> adds <c>site-split--media-start</c> and moves the media only visually, so the text stays first
    /// in reading order, for example to alternate the photos of a day-by-day itinerary.
    /// </summary>
    [Parameter]
    public SplitMediaSide MediaSide { get; set; }

    /// <summary>
    /// Gets or sets where the shorter column lies beside the taller one from the breakpoint on. The default is
    /// <see cref="SiteAlignment.Start" />: both columns start at the top, so a photo stays beside the heading and the first paragraph it
    /// illustrates instead of floating down a long text. <see cref="SiteAlignment.Center" /> adds <c>site-split--align-center</c> and
    /// centres the columns on each other, for a short tagline beside a photo.
    /// </summary>
    [Parameter]
    public SiteAlignment Alignment { get; set; }

    private string RootClass => (MediaSide, Alignment) switch
    {
        (SplitMediaSide.Start, SiteAlignment.Center) => "site-split site-split--media-start site-split--align-center",
        (SplitMediaSide.Start, _) => "site-split site-split--media-start",
        (_, SiteAlignment.Center) => "site-split site-split--align-center",
        _ => "site-split",
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Media);
        if (!Enum.IsDefined(MediaSide))
        {
            throw new ArgumentOutOfRangeException(nameof(MediaSide), MediaSide, "The split media side is not defined.");
        }

        if (!Enum.IsDefined(Alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(Alignment), Alignment, "The split alignment is not defined.");
        }
    }
}
