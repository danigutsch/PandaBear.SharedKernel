namespace SharedKernel.PageSections;

/// <summary>An unvalidated quote of a section.</summary>
/// <param name="Text">The quoted words; required.</param>
/// <param name="Cite">The optional name of who said them.</param>
public sealed record PageQuoteDefinition(string? Text, string? Cite);
