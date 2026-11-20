using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A round call-to-action link fixed in the bottom corner of the window, such as a chat link. Its content is a decorative icon; the
/// link is named by <see cref="Label" />. While it is on the page, the end of the page and every element scrolled into view keep
/// clear of it.
/// </summary>
public sealed partial class FloatingLink
{
    /// <summary>Gets or sets the link target.</summary>
    [Parameter]
    [EditorRequired]
    public LinkTarget? Href { get; set; }

    /// <summary>Gets or sets the accessible name, for example "Chat on WhatsApp". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the icon, such as an inline <c>svg</c>; it is hidden from assistive technology.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Href);
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
    }
}
