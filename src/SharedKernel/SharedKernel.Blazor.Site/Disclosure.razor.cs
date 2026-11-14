using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A native <c>details</c> and <c>summary</c> disclosure. Stack several for an accordion, or give them the same <c>name</c>
/// attribute so the browser keeps one open at a time; no script is involved.
/// </summary>
public sealed partial class Disclosure
{
    /// <summary>Gets or sets the always-visible summary text.</summary>
    [Parameter]
    [EditorRequired]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the disclosure starts open.</summary>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the hidden content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() => ArgumentException.ThrowIfNullOrWhiteSpace(Summary);
}
