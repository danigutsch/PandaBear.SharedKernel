using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A link that is the first focusable element of the page and moves keyboard focus past the header to the main content.
/// The host renders the target, for example <c>&lt;main id="main-content" tabindex="-1"&gt;</c>.
/// </summary>
public sealed partial class SkipLink
{
    /// <summary>Gets or sets the id of the element to skip to, without <c>#</c>.</summary>
    [Parameter]
    public string TargetId { get; set; } = "main-content";

    /// <summary>Gets or sets the link text, for example "Skip to content". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() => ArgumentException.ThrowIfNullOrWhiteSpace(TargetId);
}
