using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>An auto-fit grid for <see cref="Card" /> components.</summary>
public sealed partial class CardGrid
{
    /// <summary>Gets or sets the cards.</summary>
    [Parameter]
    public RenderFragment? ChildContent
    {
        get; set;
    }
}
