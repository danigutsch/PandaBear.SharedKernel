using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// A link styled as a button. A link that opens a new tab always carries <c>rel="noopener noreferrer"</c> and a visually hidden
/// hint, so people using assistive technology know it leaves the page. The target is a <see cref="LinkTarget" />, so only
/// relative addresses and the http, https, mailto and tel schemes reach the markup.
/// </summary>
public sealed partial class ButtonLink
{
    /// <summary>Gets or sets the link target.</summary>
    [Parameter]
    [EditorRequired]
    public LinkTarget? Href { get; set; }

    /// <summary>Gets or sets the visual style. The default is <see cref="ButtonVariant.Primary" />.</summary>
    [Parameter]
    public ButtonVariant Variant { get; set; }

    /// <summary>
    /// Gets or sets the size. The default is <see cref="ButtonSize.Regular" />; <see cref="ButtonSize.Compact" /> adds
    /// <c>site-button--compact</c>. A card's more look of the same variant and size is identical.
    /// </summary>
    [Parameter]
    public ButtonSize Size { get; set; }

    /// <summary>Gets or sets whether the link opens in a new tab.</summary>
    [Parameter]
    public bool NewTab { get; set; }

    /// <summary>Gets or sets the hint announced for a new-tab link. Caller-supplied so it can be localized.</summary>
    [Parameter]
    public string NewTabHint { get; set; } = "(opens in a new tab)";

    /// <summary>Gets or sets the link text.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Href);
        ButtonStyle.RequireSize(Size, nameof(Size));
    }
}
