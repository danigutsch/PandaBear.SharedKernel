using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A grid of fact cards, such as a tour's basic information: a <c>dl</c> whose cards are <c>div</c> groups of the <c>dt</c> label, with
/// the optional decorative icon inside it, and one <c>dd</c> per value, holding a link when the value has one. The cards are centred and bordered like the cells of a table.
/// Below the <c>48rem</c> breakpoint they fill columns at least 11rem wide; from it on the grid has the balanced column count of
/// <c>site-fact-grid--columns-1</c> to <c>site-fact-grid--columns-4</c>: as many columns as cards up to four, then the fewest rows of
/// at most four cards with the cards spread as evenly as possible over them.
/// </summary>
public sealed partial class FactGrid
{
    private const int MaxColumns = 4;

    /// <summary>Gets or sets the cards, in reading order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<FactCard> Cards { get; set; } = [];

    private string RootClass => Cards.Count == 0 ? "site-fact-grid" : $"site-fact-grid site-fact-grid--columns-{Columns(Cards.Count)}";

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Cards" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A card is null, has a blank label, or has no values, a null value or a value with blank text.
    /// </exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Cards);
        foreach (var card in Cards)
        {
            if (card is null)
            {
                throw new ArgumentException("A fact card is missing.", nameof(Cards));
            }

            if (string.IsNullOrWhiteSpace(card.Label))
            {
                throw new ArgumentException("Every fact card needs a label.", nameof(Cards));
            }

            if (card.Values is null || card.Values.Count == 0)
            {
                throw new ArgumentException($"The fact card '{card.Label}' needs at least one value.", nameof(Cards));
            }

            if (card.Values.Any(value => value is null || string.IsNullOrWhiteSpace(value.Text)))
            {
                throw new ArgumentException($"The fact card '{card.Label}' has a missing or blank value.", nameof(Cards));
            }
        }
    }

    private static int Columns(int count)
    {
        var rows = (count + MaxColumns - 1) / MaxColumns;
        return (count + rows - 1) / rows;
    }
}
