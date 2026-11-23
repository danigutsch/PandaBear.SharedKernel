using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The lowest price of an offering in a line of text, such as "a partir de 1.234,50 €": a <c>p</c> (<c>site-from-price</c>) of the
/// <see cref="Format" /> split at its <c>{0}</c>, with the amount in a <c>&lt;data value&gt;</c> (<c>site-from-price__amount</c>) that is
/// bold and never wraps. The text around the placeholder is written as it is, never passed to <see cref="string.Format(string, object)" />.
/// </summary>
public sealed partial class FromPrice
{
    private const string Placeholder = "{0}";

    private string _before = string.Empty;

    private string _after = string.Empty;

    /// <summary>
    /// Gets or sets the line with exactly one <c>{0}</c> where the amount goes, such as "a partir de {0}" or "from {0} per person". The
    /// text is the application's.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public string Format { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount as the page shows it, such as "1.234,50 €", formatted by the application. It must not be blank.</summary>
    [Parameter]
    [EditorRequired]
    public string AmountText { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount as a number, written invariantly into <c>&lt;data value&gt;</c>.</summary>
    [Parameter]
    [EditorRequired]
    public decimal Value { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// <see cref="Format" /> does not hold exactly one <c>{0}</c>, or holds it escaped (<c>{{0}}</c>), or <see cref="AmountText" /> is
    /// blank.
    /// </exception>
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Format);
        ArgumentException.ThrowIfNullOrWhiteSpace(AmountText);
        var index = Format.IndexOf(Placeholder, StringComparison.Ordinal);
        var end = index + Placeholder.Length;
        if (index < 0
            || Format.IndexOf(Placeholder, end, StringComparison.Ordinal) >= 0
            || (index > 0 && Format[index - 1] == '{')
            || (end < Format.Length && Format[end] == '}'))
        {
            throw new ArgumentException("The format must hold exactly one {0}, not escaped as {{0}}, where the amount goes.", nameof(Format));
        }

        _before = Format[..index];
        _after = Format[end..];
    }
}
