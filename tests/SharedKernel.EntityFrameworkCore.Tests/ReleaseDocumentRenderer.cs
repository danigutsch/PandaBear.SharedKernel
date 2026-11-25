using PandaBear.DocumentRendering;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Renders a deterministic neutral release document.</summary>
internal sealed class ReleaseDocumentRenderer : IDocumentRenderer
{
    /// <inheritdoc />
    public byte[] Render(DocumentRenderRequest request) => "release ready"u8.ToArray();
}
