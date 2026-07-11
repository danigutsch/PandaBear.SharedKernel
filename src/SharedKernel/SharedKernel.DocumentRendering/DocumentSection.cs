namespace SharedKernel.DocumentRendering;

/// <summary>
/// Defines an ordered semantic section of a document.
/// </summary>
public sealed record DocumentSection(string Heading, IReadOnlyList<DocumentField> Fields);
