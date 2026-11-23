using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The short credit under an image: a <c>small</c> element (<c>site-image-credit</c>, a muted line of small text) of the credit, linked to
/// the full attribution when <see cref="CreditsHref" /> is set, then the licence after a space, linked with <c>rel="license"</c> when
/// <see cref="LicenceHref" /> is set, as Creative Commons recommends. An <see cref="ImageGrid" /> renders one for a
/// <see cref="GalleryImage.Credit" /> inside the image's <c>figcaption</c>.
/// </summary>
public sealed partial class ImageCreditCaption
{
    /// <summary>Gets or sets the credit, such as "Photo: Ana Lima". It must not be blank.</summary>
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the licence's short name, such as "CC BY 4.0"; null or blank shows none.</summary>
    [Parameter]
    public string? LicenceText { get; set; }

    /// <summary>Gets or sets the licence's address, such as the Creative Commons deed; it links <see cref="LicenceText" />, which it needs.</summary>
    [Parameter]
    public LinkTarget? LicenceHref { get; set; }

    /// <summary>Gets or sets the address of the full attribution, such as the site's credits page; it links <see cref="Text" />.</summary>
    [Parameter]
    public LinkTarget? CreditsHref { get; set; }

    private bool HasLicence => !string.IsNullOrWhiteSpace(LicenceText);

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><see cref="Text" /> is blank, or <see cref="LicenceHref" /> is set without <see cref="LicenceText" />.</exception>
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Text);
        if (LicenceHref is not null && !HasLicence)
        {
            throw new ArgumentException("A licence address needs the licence text that it links.", nameof(LicenceText));
        }
    }
}
