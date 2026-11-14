using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The page introduction with the one level-1 heading, a subtitle, an optional image and an actions slot.
/// The image is loaded eagerly and with high fetch priority because it is usually the largest visible element.
/// </summary>
public sealed partial class Hero
{
    /// <summary>Gets or sets the level-1 heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the text under the heading.</summary>
    [Parameter]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the image, loaded eagerly with <c>fetchpriority="high"</c>. A <see cref="ResponsiveImage" /> with sources
    /// renders a <c>picture</c> with <c>srcset</c> and <c>sizes</c>; one without renders a single <c>img</c>.
    /// </summary>
    [Parameter]
    public ResponsiveImage? Image { get; set; }

    /// <summary>Gets or sets the buttons or links under the subtitle.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
    }
}
