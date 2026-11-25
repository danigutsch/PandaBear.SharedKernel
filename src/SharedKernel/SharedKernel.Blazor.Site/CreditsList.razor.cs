using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// The credits of a site's images, such as on a credits page: a <c>ul</c> (<c>site-credits-list</c>) with <c>role="list"</c>, because
/// Safari drops the list semantics of a list without markers. Each <see cref="CreditItem" /> is an <c>li</c> of an optional lazy
/// thumbnail beside its title linked to the source, its author linked to the author's page (or to the source when there is no title),
/// its licence linked to the licence's deed with <c>rel="license"</c>, and what was changed, each a <c>p</c>. An empty list renders
/// nothing.
/// </summary>
public sealed partial class CreditsList
{
    /// <summary>Gets or sets the credits, in reading order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CreditItem> Items { get; set; } = [];

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Items" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A credit is missing, has a blank author or licence text, or has no title but links both its author and its source.
    /// </exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        foreach (var item in Items)
        {
            CreditItem.Require(item, nameof(Items));
        }
    }
}
