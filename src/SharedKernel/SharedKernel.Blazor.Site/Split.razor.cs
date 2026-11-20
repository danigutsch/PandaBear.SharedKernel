using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// Text beside media, such as photos, without a heading of its own: a plain <c>div</c>, never a landmark, with the text in
/// <c>site-split__main</c> and the media in <c>site-split__media</c>, in reading order. From the <c>48rem</c> breakpoint on they are two
/// equal columns, below it the media follows the text. <see cref="Section.Media" /> uses it for a section's split.
/// </summary>
public sealed partial class Split
{
    /// <summary>Gets or sets the text.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the media beside the text, which fills its column.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? Media { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() => ArgumentNullException.ThrowIfNull(Media);
}
