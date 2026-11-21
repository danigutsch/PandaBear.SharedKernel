using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A grid of fact cards, such as a tour's basic information: a <c>dl</c> whose cards are <c>div</c> groups of the <c>dt</c> label, with
/// the optional decorative icon inside it, and one <c>dd</c> per value. The cards are centred, bordered like the cells of a table, in
/// columns at least 11rem wide that share the full width of the container.
/// </summary>
public sealed partial class FactGrid
{
    /// <summary>Gets or sets the cards, in reading order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<FactCard> Cards { get; set; } = [];

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Cards" /> is null.</exception>
    /// <exception cref="ArgumentException">A card is null, has a blank label, or has no values or a blank value.</exception>
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

            if (card.Values.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException($"The fact card '{card.Label}' has a blank value.", nameof(Cards));
            }
        }
    }
}
