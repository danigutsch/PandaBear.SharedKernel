using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

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
    protected override void OnParametersSet() => ArgumentNullException.ThrowIfNull(Href);

    private string VariantClass => Variant switch
    {
        ButtonVariant.Secondary => "site-button site-button--secondary",
        ButtonVariant.Outline => "site-button site-button--outline",
        _ => "site-button site-button--primary",
    };
}
