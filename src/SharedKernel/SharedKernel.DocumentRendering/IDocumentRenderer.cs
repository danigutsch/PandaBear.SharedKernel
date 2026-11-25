namespace SharedKernel.DocumentRendering;

/// <summary>
/// Renders canonical structured document data to an immutable payload.
/// </summary>
public interface IDocumentRenderer
{
    /// <summary>
    /// Renders a document request deterministically.
    /// </summary>
    /// <param name="request">Canonical document data.</param>
    /// <returns>The rendered artifact content.</returns>
    byte[] Render(DocumentRenderRequest request);
}
