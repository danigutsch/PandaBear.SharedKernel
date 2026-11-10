namespace PandaBear.Blazor.Site;

/// <summary>One row of a <see cref="FactsTable" />.</summary>
/// <param name="Label">The row header, for example "Duration".</param>
/// <param name="Value">The value, for example "8 days".</param>
public sealed record FactRow(string Label, string Value);
