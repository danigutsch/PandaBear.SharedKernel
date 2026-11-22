using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>One card of a <see cref="FactGrid" />: a label with one or more values and an optional icon.</summary>
/// <param name="Label">The label, the card's term, for example "Duration". It must not be blank.</param>
/// <param name="Values">
/// The values, one definition each, for example "8 days" and "7 nights", or an e-mail address that links to it. At least one, none
/// with blank text.
/// </param>
/// <param name="Icon">
/// The optional icon above the label, such as an inline <c>svg</c>. It is decorative, hidden from assistive technology, and takes the
/// primary colour: an <c>svg</c> without a <c>fill</c> attribute is filled with it, and one drawn with <c>currentColor</c> uses it.
/// </param>
public sealed record FactCard(string Label, IReadOnlyList<FactValue> Values, RenderFragment? Icon = null);
