using System.Text;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>
/// Renders requests to HTML text for the paragraph and compatibility tests.
/// </summary>
internal static class DocumentRenderingHelper
{
    /// <summary>
    /// Renders a request with the default renderer and decodes the UTF-8 bytes.
    /// </summary>
    internal static string RenderHtml(DocumentRenderRequest request) =>
        Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

    /// <summary>
    /// Creates a request that holds one section.
    /// </summary>
    internal static DocumentRenderRequest RequestWith(DocumentSection section) =>
        new("pt-BR", "Contrato", [section], null);

    /// <summary>
    /// Creates an action that renders a section holding only <paramref name="block"/> as its blocks.
    /// </summary>
    internal static Action RenderBlock(DocumentBlock block) =>
        () => RenderHtml(RequestWith(new DocumentSection("S", []) { Blocks = [block] }));
}
