namespace SharedKernel.DocumentRendering;

/// <summary>
/// Supplies canonical structured data to a document renderer.
/// </summary>
public sealed record DocumentRenderRequest(
    string Language,
    string Title,
    IReadOnlyList<DocumentSection> Sections,
    DocumentBrandingSnapshot? Branding);
