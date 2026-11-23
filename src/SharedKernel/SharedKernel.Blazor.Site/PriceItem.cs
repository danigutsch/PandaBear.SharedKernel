namespace PandaBear.Blazor.Site;

/// <summary>One line of a <see cref="PriceList" />: what is priced, the amount as the application formats it, and its machine value.</summary>
/// <param name="Label">What is priced, such as "Double room". It must not be blank.</param>
/// <param name="AmountText">
/// The amount as the page shows it, such as "1.234,50 €", formatted by the application for the page's culture and currency. It must not be
/// blank.
/// </param>
/// <param name="Value">
/// The amount as a number, written invariantly into <c>&lt;data value&gt;</c> so machines read it whatever the page's culture;
/// <see langword="null" /> for an amount that is not a number, such as "On request", which renders without a <c>data</c> element.
/// </param>
/// <param name="Note">An optional note on the line, such as "per person".</param>
public sealed record PriceItem(string Label, string AmountText, decimal? Value, string? Note = null);
