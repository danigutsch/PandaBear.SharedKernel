using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The page footer (<c>contentinfo</c> landmark) with a content slot and a legal line, its lines a small gap apart. Its
/// <see cref="Tone" /> owns its background, spacing, colour scheme and focus ring; below the <c>48rem</c> breakpoint its links are at
/// least 2.75rem (44px) tall tap targets.
/// </summary>
public sealed partial class SiteFooter
{
    /// <summary>Gets or sets the main footer content, for example contact data and links.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the legal line, for example the copyright notice.</summary>
    [Parameter]
    public RenderFragment? Legal { get; set; }

    /// <summary>Gets or sets the background. The default is <see cref="FooterTone.Default" />.</summary>
    [Parameter]
    public FooterTone Tone { get; set; }

    /// <summary>
    /// Gets or sets the alignment of the footer's lines. The default is <see cref="SiteAlignment.Start" />;
    /// <see cref="SiteAlignment.Center" /> adds <c>site-footer--text-center</c>.
    /// </summary>
    [Parameter]
    public SiteAlignment TextAlignment { get; set; }

    private string RootClass =>
        "site-footer"
        + (Tone == FooterTone.Primary ? " site-footer--primary" : string.Empty)
        + (TextAlignment == SiteAlignment.Center ? " site-footer--text-center" : string.Empty);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Enum.IsDefined(Tone))
        {
            throw new ArgumentOutOfRangeException(nameof(Tone), Tone, "The footer tone is not defined.");
        }

        if (!Enum.IsDefined(TextAlignment))
        {
            throw new ArgumentOutOfRangeException(nameof(TextAlignment), TextAlignment, "The text alignment is not defined.");
        }
    }
}
