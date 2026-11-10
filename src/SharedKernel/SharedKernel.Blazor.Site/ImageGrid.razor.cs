using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A grid of images, each a <c>figure</c> with an optional caption. Images load lazily except the first <see cref="EagerCount" />,
/// and every image carries its width and height so the layout does not shift while it loads.
/// </summary>
public sealed partial class ImageGrid
{
    /// <summary>Gets or sets the images.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<GalleryImage> Images
    {
        get; set;
    } = [];

    /// <summary>Gets or sets how many leading images load eagerly, for example those visible without scrolling. The default is 0.</summary>
    [Parameter]
    public int EagerCount
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Images);
        ArgumentOutOfRangeException.ThrowIfNegative(EagerCount);
        foreach (var image in Images)
        {
            ArgumentNullException.ThrowIfNull(image, nameof(Images));
            ArgumentNullException.ThrowIfNull(image.Src, nameof(Images));
            ImageRules.Check(image.Src, image.Alt, image.Width, image.Height, "GalleryImage.", nameof(Images));
        }
    }
}
