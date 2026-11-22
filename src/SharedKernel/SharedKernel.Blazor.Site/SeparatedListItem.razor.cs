using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// One item of a <see cref="SeparatedList" />: an <c>li</c> (<c>site-separated-list__item</c>) kept together on its line, after a bar
/// when it is not the first. It throws <see cref="InvalidOperationException" /> outside a <see cref="SeparatedList" />.
/// </summary>
public sealed partial class SeparatedListItem
{
    /// <summary>Gets or sets the item's content, such as a label and a link.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the list the item belongs to.</summary>
    [CascadingParameter]
    internal SeparatedList? List { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (List is null)
        {
            throw new InvalidOperationException("A SeparatedListItem must be inside a SeparatedList.");
        }
    }
}
