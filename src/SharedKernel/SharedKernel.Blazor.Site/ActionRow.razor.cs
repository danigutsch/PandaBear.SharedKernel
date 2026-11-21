using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A row of actions, such as the buttons that close a page: a plain <c>div</c>, never a landmark, that keeps its children in reading
/// order. From the <c>48rem</c> breakpoint on they are one wrapping row spread across the width, the first at the start and the last at
/// the end; below it they are stacked and centred. A <see cref="ButtonLink" /> keeps its button look and a plain link stays a link in
/// the primary colour.
/// </summary>
public sealed partial class ActionRow
{
    /// <summary>Gets or sets the actions, such as <see cref="ButtonLink" /> components and links.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }
}
