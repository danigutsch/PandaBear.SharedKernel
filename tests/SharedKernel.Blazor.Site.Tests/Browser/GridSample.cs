using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>A page for the card grid tests: eight short cards in the gutter container, in a grid with the given column widths.</summary>
public sealed class GridSample : ComponentBase
{
    /// <summary>Gets or sets the smallest column width.</summary>
    [Parameter]
    public CardColumnWidth Min { get; set; }

    /// <summary>Gets or sets the largest card width.</summary>
    [Parameter]
    public CardColumnWidth? Max { get; set; }

    /// <summary>Gets or sets the alignment of the cards in their columns.</summary>
    [Parameter]
    public SiteAlignment Alignment { get; set; }

    /// <summary>Gets the parameters of the sample.</summary>
    /// <param name="min">The smallest column width.</param>
    /// <param name="max">The largest card width.</param>
    /// <param name="alignment">The alignment of the cards in their columns.</param>
    /// <returns>The parameters by name.</returns>
    public static Dictionary<string, object?> Parameters(CardColumnWidth min, CardColumnWidth? max, SiteAlignment alignment = SiteAlignment.Start) => new()
    {
        [nameof(Min)] = min,
        [nameof(Max)] = max,
        [nameof(Alignment)] = alignment,
    };

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "class", "site-container");
        builder.OpenComponent<CardGrid>(2);
        builder.AddComponentParameter(3, nameof(CardGrid.MinColumnWidth), Min);
        builder.AddComponentParameter(4, nameof(CardGrid.MaxColumnWidth), Max);
        builder.AddComponentParameter(5, nameof(CardGrid.ItemAlignment), Alignment);
        builder.AddComponentParameter(6, nameof(CardGrid.ChildContent), (RenderFragment)(grid =>
        {
            foreach (var index in Enumerable.Range(1, 8))
            {
                grid.OpenComponent<Card>(0);
                grid.AddComponentParameter(1, nameof(Card.Heading), "Tour " + index);
                grid.CloseComponent();
            }
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }
}
