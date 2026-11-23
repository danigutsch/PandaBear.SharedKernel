using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A short status, such as "Open" or "Sold out", after the GOV.UK tag: a <c>strong</c> element (<c>site-status</c>) whose
/// <see cref="Tone" /> sets its background and text colour, with an optional visually hidden <see cref="Label" /> before the text. It is
/// never interactive, and its text always says the status, so colour is never the only cue.
/// </summary>
public sealed partial class StatusBadge
{
    /// <summary>Gets or sets the status text, such as "Sold out". It must not be blank.</summary>
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the tone. The default is <see cref="StatusTone.Neutral" />.</summary>
    [Parameter]
    public StatusTone Tone { get; set; }

    /// <summary>
    /// Gets or sets a visually hidden prefix, such as "Status:", read before the text and followed by a space, for a badge whose meaning
    /// the page does not otherwise give. Null or blank renders none.
    /// </summary>
    [Parameter]
    public string? Label { get; set; }

    private bool HasLabel => !string.IsNullOrWhiteSpace(Label);

    private string RootClass => Tone switch
    {
        StatusTone.Info => "site-status site-status--info",
        StatusTone.Success => "site-status site-status--success",
        StatusTone.Warning => "site-status site-status--warning",
        StatusTone.Danger => "site-status site-status--danger",
        _ => "site-status site-status--neutral",
    };

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><see cref="Text" /> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Tone" /> is not defined.</exception>
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Text);
        if (!Enum.IsDefined(Tone))
        {
            throw new ArgumentOutOfRangeException(nameof(Tone), Tone, "The status tone is not defined.");
        }
    }
}
