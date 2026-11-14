using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>The page footer (<c>contentinfo</c> landmark) with a content slot and a legal line.</summary>
public sealed partial class SiteFooter
{
    /// <summary>Gets or sets the main footer content, for example contact data and links.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the legal line, for example the copyright notice.</summary>
    [Parameter]
    public RenderFragment? Legal { get; set; }
}
