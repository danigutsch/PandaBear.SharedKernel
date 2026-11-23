using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The prices of an offering, after the GOV.UK summary list: a <c>dl</c> (<c>site-price-list__items</c>) of rows, each a <c>div</c> of the
/// <c>dt</c> label, the <c>dd</c> amount, which holds a <c>&lt;data value&gt;</c> when the amount is a number and never wraps, and an
/// optional <c>dd</c> note, then the optional <see cref="Conditions" />. With <see cref="HeadingId" /> the whole is a group named by that
/// heading.
/// </summary>
public sealed partial class PriceList
{
    /// <summary>Gets or sets the price lines, in reading order.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<PriceItem> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets the id of the heading that names the prices, such as a section's "Prices". With it the root is
    /// <c>role="group"</c> with <c>aria-labelledby</c>, so the lines and the conditions are announced as one named group. It must not
    /// be empty or hold white space.
    /// </summary>
    [Parameter]
    public string? HeadingId { get; set; }

    /// <summary>Gets or sets the conditions after the lines, such as the deposit and the payment terms, in <c>site-price-list__conditions</c>.</summary>
    [Parameter]
    public RenderFragment? Conditions { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><see cref="Items" /> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A line is missing or has a blank label or amount, or <see cref="HeadingId" /> is empty or holds white space.
    /// </exception>
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Items);
        foreach (var item in Items)
        {
            if (item is null)
            {
                throw new ArgumentException("A price line is missing.", nameof(Items));
            }

            if (string.IsNullOrWhiteSpace(item.Label) || string.IsNullOrWhiteSpace(item.AmountText))
            {
                throw new ArgumentException("Every price line needs a label and an amount.", nameof(Items));
            }
        }

        ElementId.Require(HeadingId, nameof(HeadingId));
    }
}
