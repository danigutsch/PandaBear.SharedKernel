using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The page introduction with the one level-1 heading, a subtitle, an optional image and an actions slot.
/// The image is loaded eagerly because it is usually the largest visible element.
/// </summary>
public sealed partial class Hero
{
    /// <summary>Gets or sets the level-1 heading.</summary>
    [Parameter]
    [EditorRequired]
    public string Title
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the text under the heading.</summary>
    [Parameter]
    public string? Subtitle
    {
        get; set;
    }

    /// <summary>Gets or sets the image address. When set, <see cref="ImageAlt" /> is required.</summary>
    [Parameter]
    public LinkTarget? ImageSrc
    {
        get; set;
    }

    /// <summary>Gets or sets the accessibility decision for the image: a description, or decorative.</summary>
    [Parameter]
    public ImageAlternative? ImageAlt
    {
        get; set;
    }

    /// <summary>Gets or sets the intrinsic image width in pixels.</summary>
    [Parameter]
    public int? ImageWidth
    {
        get; set;
    }

    /// <summary>Gets or sets the intrinsic image height in pixels.</summary>
    [Parameter]
    public int? ImageHeight
    {
        get; set;
    }

    /// <summary>Gets or sets the buttons or links under the subtitle.</summary>
    [Parameter]
    public RenderFragment? Actions
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ImageRules.Check(ImageSrc, ImageAlt, ImageWidth, ImageHeight, "Image");
    }
}
