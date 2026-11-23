using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The credits of a site's images, such as on a credits page: a <c>ul</c> (<c>site-credits-list</c>) with <c>role="list"</c>, because
/// Safari drops the list semantics of a list without markers. Each <see cref="CreditItem" /> is an <c>li</c> of an optional lazy
/// thumbnail beside its title linked to the source, its author, its licence linked to the licence's deed with <c>rel="license"</c>, and
/// what was changed, each a <c>p</c>. An empty list renders nothing.
/// </summary>
public sealed partial class CreditsList
{
    /// <summary>Gets or sets the credits, in reading order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CreditItem> Items { get; set; } = [];

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Items" /> is null.</exception>
    /// <exception cref="ArgumentException">A credit is missing or has a blank author or licence text.</exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        foreach (var item in Items)
        {
            if (item is null)
            {
                throw new ArgumentException("A credit is missing.", nameof(Items));
            }

            if (string.IsNullOrWhiteSpace(item.Author) || string.IsNullOrWhiteSpace(item.LicenceText))
            {
                throw new ArgumentException("Every credit needs an author and a licence text.", nameof(Items));
            }
        }
    }
}
