using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// An auto-fill grid for <see cref="Card" /> components: columns at least <see cref="MinColumnWidth" /> wide (or the whole width when that
/// is less), as wide as the row allows or at most <see cref="MaxColumnWidth" />, <see cref="Gap" /> apart. The options are modifier classes
/// that set the <c>--site-card-min-width</c>, <c>--site-card-max-width</c> and <c>--site-card-gap</c> tokens, so no style attribute is
/// written.
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
    /// Gets or sets the largest column width, at least <see cref="MinColumnWidth" />. <see langword="null" />, the default, lets the
    /// columns share the row, keeping empty columns so a lone card has the width of one column. A width (<c>site-card-grid--max-narrow</c>,
    /// <c>--max-medium</c> or <c>--max-wide</c>) caps the columns and centres them in the row, dropping empty columns.
    /// </summary>
    [Parameter]
    public CardColumnWidth? MaxColumnWidth { get; set; }

    /// <summary>
    /// Gets or sets the space between the cards, a step of the spacing scale. The default is <see cref="SiteSpace.Space4" />; another step
    /// adds <c>site-card-grid--gap-{step}</c>.
    /// </summary>
    [Parameter]
    public SiteSpace Gap { get; set; } = SiteSpace.Space4;

    private string RootClass
    {
        get
        {
            var classes = "site-card-grid";
            classes += MinColumnWidth == CardColumnWidth.Narrow ? string.Empty : " site-card-grid--min-" + Name(MinColumnWidth);
            classes += MaxColumnWidth is { } max ? " site-card-grid--max-" + Name(max) : string.Empty;
            classes += Gap == SiteSpace.Space4 ? string.Empty : " site-card-grid--gap-" + ((int)Gap).ToString(CultureInfo.InvariantCulture);
            return classes;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Enum.IsDefined(MinColumnWidth))
        {
            throw new ArgumentOutOfRangeException(nameof(MinColumnWidth), MinColumnWidth, "The column width is not defined.");
        }

        if (MaxColumnWidth is { } max && !Enum.IsDefined(max))
        {
            throw new ArgumentOutOfRangeException(nameof(MaxColumnWidth), max, "The column width is not defined.");
        }

        if (!Enum.IsDefined(Gap))
        {
            throw new ArgumentOutOfRangeException(nameof(Gap), Gap, "The spacing step is not defined.");
        }

        if (MaxColumnWidth < MinColumnWidth)
        {
            throw new ArgumentException("The largest column width must be at least the smallest.", nameof(MaxColumnWidth));
        }
    }

    private static string Name(CardColumnWidth width) => width switch
    {
        CardColumnWidth.Medium => "medium",
        CardColumnWidth.Wide => "wide",
        _ => "narrow",
    };
}
