using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>A card (<c>article</c>) with an optional image, a heading that can be a link, text and an actions slot.</summary>
public sealed partial class Card
{
    /// <summary>Gets or sets the heading text.</summary>
    [Parameter]
    [EditorRequired]
    public string Title
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the address the heading links to. Without it the heading is plain text.</summary>
    [Parameter]
    public LinkTarget? Href
    {
        get; set;
    }

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 3.</summary>
    [Parameter]
    public int HeadingLevel
    {
        get; set;
    } = 3;

    /// <summary>
    /// Gets or sets the image address, root-relative or https. When set, <see cref="ImageAlt" />, <see cref="ImageWidth" /> and
    /// <see cref="ImageHeight" /> are required.
    /// </summary>
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

    /// <summary>Gets or sets the intrinsic image width in pixels; required with an image.</summary>
    [Parameter]
    public int? ImageWidth
    {
        get; set;
    }

    /// <summary>Gets or sets the intrinsic image height in pixels; required with an image.</summary>
    [Parameter]
    public int? ImageHeight
    {
        get; set;
    }

    /// <summary>Gets or sets the card text.</summary>
    [Parameter]
    public RenderFragment? ChildContent
    {
        get; set;
    }

    /// <summary>Gets or sets the buttons or links at the bottom of the card.</summary>
    [Parameter]
    public RenderFragment? Actions
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        Heading.RequireLevel(HeadingLevel, nameof(HeadingLevel));
        ImageRules.Check(ImageSrc, ImageAlt, ImageWidth, ImageHeight, "Image");
    }
}
