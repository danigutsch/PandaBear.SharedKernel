using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A full-width band around content of the content width: a <see cref="Section" />, or content without a heading such as a tagline
/// and a <see cref="Byline" />. The band is a plain <c>div</c>, never a landmark; a section inside it keeps its own semantics.
/// </summary>
public sealed partial class Band
{
    /// <summary>Gets or sets the background. The default is <see cref="BandTone.Default" />.</summary>
    [Parameter]
    public BandTone Tone { get; set; }

    /// <summary>
    /// Gets or sets the photo behind the content of a <see cref="BandTone.Image" /> band, loaded lazily. Required for that tone and
    /// refused for the others, so it is never silently ignored. It is usually decorative.
    /// </summary>
    [Parameter]
    public ResponsiveImage? Image { get; set; }

    /// <summary>Gets or sets the content.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the alignment of the band's text. <see langword="null" />, the default, inherits the nearest aligned ancestor, or the
    /// start. <see cref="SiteAlignment.Center" /> adds <c>site-band--text-center</c>, which centres the text of the band and of a section
    /// in it that does not set its own, as <see cref="Section.TextAlignment" /> does, for content without a heading such as a tagline and
    /// a <see cref="Byline" />. <see cref="SiteAlignment.Start" /> adds <c>site-band--text-start</c>.
    /// </summary>
    [Parameter]
    public SiteAlignment? TextAlignment { get; set; }

    private string RootClass => TextAlignment switch
    {
        SiteAlignment.Center => ToneClass + " site-band--text-center",
        SiteAlignment.Start => ToneClass + " site-band--text-start",
        _ => ToneClass,
    };

    private string ToneClass => Tone switch
    {
        BandTone.Muted => "site-band site-band--muted",
        BandTone.Primary => "site-band site-band--primary",
        BandTone.Image => "site-band site-band--image",
        _ => "site-band site-band--default",
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Enum.IsDefined(Tone))
        {
            throw new ArgumentOutOfRangeException(nameof(Tone), Tone, "The band tone is not defined.");
        }

        if (TextAlignment is { } alignment && !Enum.IsDefined(alignment))
        {
            throw new ArgumentOutOfRangeException(nameof(TextAlignment), alignment, "The text alignment is not defined.");
        }

        if (Tone == BandTone.Image && Image is null)
        {
            throw new ArgumentException("An image band needs an image.", nameof(Image));
        }

        if (Tone != BandTone.Image && Image is not null)
        {
            throw new ArgumentException("Only an image band has an image; set the tone to Image or leave the image out.", nameof(Image));
        }
    }
}
