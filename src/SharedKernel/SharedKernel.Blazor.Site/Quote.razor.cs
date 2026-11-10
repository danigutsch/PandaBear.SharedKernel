using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>A testimonial: a <c>blockquote</c> and the <c>cite</c> of its source in a <c>figcaption</c>.</summary>
public sealed partial class Quote
{
    /// <summary>Gets or sets who said it, for example "Ana, Lisbon".</summary>
    [Parameter]
    public string? Cite
    {
        get; set;
    }

    /// <summary>Gets or sets the quoted text.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent
    {
        get; set;
    }
}
