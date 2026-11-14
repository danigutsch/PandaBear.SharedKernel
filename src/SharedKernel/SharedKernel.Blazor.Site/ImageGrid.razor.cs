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
    public IReadOnlyList<GalleryImage> Images { get; set; } = [];

    /// <summary>Gets or sets how many leading images load eagerly, for example those visible without scrolling. The default is 0.</summary>
    [Parameter]
    public int EagerCount { get; set; }

    /// <summary>
    /// Gets or sets whether the first image is the page's main image: it then loads eagerly with <c>fetchpriority="high"</c>,
    /// whatever <see cref="EagerCount" /> says. The default is <see langword="false" />.
    /// </summary>
    [Parameter]
    public bool PrioritizeFirstImage { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Images);
        ArgumentOutOfRangeException.ThrowIfNegative(EagerCount);
        foreach (var image in Images)
        {
            ArgumentNullException.ThrowIfNull(image, nameof(Images));
            ArgumentNullException.ThrowIfNull(image.Image, nameof(Images));
        }
    }
}
