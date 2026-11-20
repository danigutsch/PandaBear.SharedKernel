using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The page introduction with the one level-1 heading, an optional eyebrow above it and subtitle under it, an optional image and
/// an actions slot, beside each other or, as a <see cref="HeroLayout.Cover" />, the text over the image. With an eyebrow or a subtitle the heading is an <c>hgroup</c> of the eyebrow <c>p</c>, the <c>h1</c> and the
/// subtitle <c>p</c>, in reading order; with neither it is the <c>h1</c> alone. The image is loaded eagerly and with high fetch
/// priority because it is usually the largest visible element.
/// </summary>
public sealed partial class Hero
{
    /// <summary>
    /// Gets or sets the short line above the heading, such as a tagline. It is a <c>p</c>, not a heading, so it stays out of the
    /// document outline; null or blank renders nothing.
    /// </summary>
    [Parameter]
    public string? Eyebrow { get; set; }

    /// <summary>Gets or sets the level-1 heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the line under the heading. It is a <c>p</c>, not a heading; null or blank renders nothing.</summary>
    [Parameter]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the image, loaded eagerly with <c>fetchpriority="high"</c>. A <see cref="ResponsiveImage" /> with sources
    /// renders a <c>picture</c> with <c>srcset</c> and <c>sizes</c>; one without renders a single <c>img</c>.
    /// </summary>
    [Parameter]
    public ResponsiveImage? Image { get; set; }

    /// <summary>Gets or sets how the text and the image are placed. The default is <see cref="HeroLayout.Split" />.</summary>
    [Parameter]
    public HeroLayout Layout { get; set; }

    /// <summary>Gets or sets the buttons or links under the subtitle.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    private bool HasEyebrow => !string.IsNullOrWhiteSpace(Eyebrow);

    private bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        if (!Enum.IsDefined(Layout))
        {
            throw new ArgumentOutOfRangeException(nameof(Layout), Layout, "The hero layout is not defined.");
        }
    }
}
