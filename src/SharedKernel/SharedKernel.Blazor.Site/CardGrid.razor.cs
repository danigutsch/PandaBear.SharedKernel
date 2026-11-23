using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// An auto-fill grid for <see cref="Card" /> components: columns at least <see cref="MinColumnWidth" /> wide (or the whole width when that
/// is less) that share the row, <see cref="Gap" /> apart, each card at most <see cref="MaxColumnWidth" /> wide and placed in its column by
/// <see cref="ItemAlignment" />. The options are modifier classes that set the <c>--site-card-min-width</c>, <c>--site-card-max-width</c>
/// and <c>--site-card-gap</c> tokens, so no style attribute is written.
/// </summary>
public sealed partial class CardGrid
{
    /// <summary>Gets or sets the cards.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the smallest column width. The default is <see cref="CardColumnWidth.Narrow" /> (16rem, the
    /// <c>--site-card-min-width</c> token); <see cref="CardColumnWidth.Medium" /> and <see cref="CardColumnWidth.Wide" /> add
    /// <c>site-card-grid--min-medium</c> and <c>site-card-grid--min-wide</c>.
    /// </summary>
    [Parameter]
    public CardColumnWidth MinColumnWidth { get; set; }

    /// <summary>
    /// Gets or sets the largest card width, at least <see cref="MinColumnWidth" />. <see langword="null" />, the default, lets the cards
    /// fill their columns and keeps empty columns, so a lone card has the width of one column. A width (<c>site-card-grid--max-narrow</c>,
    /// <c>--max-medium</c> or <c>--max-wide</c>) caps each card in its column and drops empty columns; the number of columns still
    /// follows <see cref="MinColumnWidth" />.
    /// </summary>
    [Parameter]
    public CardColumnWidth? MaxColumnWidth { get; set; }

    /// <summary>
    /// Gets or sets the space between the cards, a step of the spacing scale. The default is <see cref="SiteSpace.Space4" />; another step
    /// adds <c>site-card-grid--gap-{step}</c>.
    /// </summary>
    [Parameter]
    public SiteSpace Gap { get; set; } = SiteSpace.Space4;

    /// <summary>
    /// Gets or sets where a card narrower than its column lies in it. The default is <see cref="SiteAlignment.Start" />;
    /// <see cref="SiteAlignment.Center" /> adds <c>site-card-grid--items-center</c>.
    /// </summary>
    [Parameter]
    public SiteAlignment ItemAlignment { get; set; }

    private string RootClass
    {
        get
        {
            var classes = "site-card-grid";
            classes += MinColumnWidth == CardColumnWidth.Narrow ? string.Empty : " site-card-grid--min-" + Name(MinColumnWidth);
            classes += MaxColumnWidth is { } max ? " site-card-grid--max-" + Name(max) : string.Empty;
            classes += Gap == SiteSpace.Space4 ? string.Empty : " site-card-grid--gap-" + ((int)Gap).ToString(CultureInfo.InvariantCulture);
            classes += ItemAlignment == SiteAlignment.Center ? " site-card-grid--items-center" : string.Empty;
            return classes;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        EnumParameter.RequireDefined(MinColumnWidth, nameof(MinColumnWidth), "The column width is not defined.");
        EnumParameter.RequireDefined(MaxColumnWidth, nameof(MaxColumnWidth), "The column width is not defined.");
        EnumParameter.RequireDefined(Gap, nameof(Gap), "The spacing step is not defined.");
        EnumParameter.RequireDefined(ItemAlignment, nameof(ItemAlignment), "The item alignment is not defined.");

        if (MaxColumnWidth is { } cap && Rem(cap) < Rem(MinColumnWidth))
        {
            throw new ArgumentException("The largest column width must be at least the smallest.", nameof(MaxColumnWidth));
        }
    }

    private static decimal Rem(CardColumnWidth width) => width switch
    {
        CardColumnWidth.Medium => 22m,
        CardColumnWidth.Wide => 32m,
        _ => 16m,
    };

    private static string Name(CardColumnWidth width) => width switch
    {
        CardColumnWidth.Medium => "medium",
        CardColumnWidth.Wide => "wide",
        _ => "narrow",
    };
}
