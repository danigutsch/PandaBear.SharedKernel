using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the card alignment tests: a band with centred text around a grid of a card without an alignment of its own (<c>#start</c>)
/// and a centred card (<c>#center</c>), each with a byline and the more look.
/// </summary>
public sealed class AlignedCardsSample : ComponentBase
{
    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.OpenComponent<Band>(1);
        builder.AddComponentParameter(2, nameof(Band.TextAlignment), (SiteAlignment?)SiteAlignment.Center);
        builder.AddComponentParameter(3, nameof(Band.ChildContent), (RenderFragment)(band =>
        {
            band.OpenComponent<CardGrid>(0);
            band.AddComponentParameter(1, nameof(CardGrid.MinColumnWidth), CardColumnWidth.Wide);
            band.AddComponentParameter(2, nameof(CardGrid.ChildContent), (RenderFragment)(grid =>
            {
                grid.AddContent(0, CardFragment("start", SiteAlignment.Start));
                grid.AddContent(1, CardFragment("center", SiteAlignment.Center));
            }));
            band.CloseComponent();
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private static RenderFragment CardFragment(string id, SiteAlignment alignment) => builder =>
    {
        builder.OpenComponent<Card>(0);
        builder.AddComponentParameter(1, "id", id);
        builder.AddComponentParameter(2, nameof(Card.Title), "Alentejo");
        builder.AddComponentParameter(3, nameof(Card.Href), LinkTarget.FromString("/tours/" + id));
        builder.AddComponentParameter(4, nameof(Card.TextAlignment), alignment);
        builder.AddComponentParameter(5, nameof(Card.MoreText), "More");
        builder.AddComponentParameter(6, nameof(Card.ChildContent), (RenderFragment)(body =>
        {
            body.OpenComponent<Byline>(0);
            body.AddComponentParameter(1, nameof(Byline.Name), "Ana");
            body.AddComponentParameter(2, nameof(Byline.JobTitle), "Guide");
            body.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
