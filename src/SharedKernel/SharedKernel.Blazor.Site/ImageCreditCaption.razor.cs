using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The short credit under an image: a <c>small</c> element (<c>site-image-credit</c>, a muted line of small text) of the
/// <see cref="Credit" />'s text, linked to the full attribution when it has a credits address, then its licence after a space, linked
/// with <c>rel="license"</c> when it has a licence address, as Creative Commons recommends. An <see cref="ImageGrid" /> renders one for a
/// <see cref="GalleryImage.Credit" /> inside the image's <c>figcaption</c>.
/// </summary>
public sealed partial class ImageCreditCaption
{
    /// <summary>Gets or sets the credit.</summary>
    [Parameter]
    [EditorRequired]
    public ImageCreditLine? Credit { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Credit" /> is null.</exception>
    /// <exception cref="ArgumentException">The credit's text is blank, or its licence address has no licence text.</exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Credit);
        ImageCreditLine.Require(Credit, nameof(Credit));
    }
}
