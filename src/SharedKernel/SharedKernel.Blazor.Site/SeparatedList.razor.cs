using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A list of short items on one line, such as the contact details or the legal links of a footer, joined by a visual bar: a
/// <c>ul</c> (<c>site-separated-list</c>) of <see cref="SeparatedListItem" /> components. The bar is drawn by the stylesheet with empty
/// alternative text, so assistive technology reads the items as a list and never the bar. Each item stays together on its line, such as
/// "Phone: +55 11 99999-0000", and breaks inside only when it is wider than the whole line.
/// </summary>
/// <remarks>
/// The list carries <c>role="list"</c>, because Safari drops the list semantics of a list without bullets. Extra attributes, such as an
/// <c>aria-label</c>, go to the <c>ul</c>; a list of links usually sits in a labelled <c>nav</c> of the caller's.
/// </remarks>
public sealed partial class SeparatedList
{
    /// <summary>Gets or sets the items, each a <see cref="SeparatedListItem" />.</summary>
    [Parameter]
    [EditorRequired]
    public RenderFragment? ChildContent { get; set; }
}
