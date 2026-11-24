using Microsoft.Playwright;
using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Qualifies the optional renderer with the caller-provisioned Chromium and qpdf runtimes.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class QualifiedChromiumPdfTests
{
    [Theory(Explicit = true)]
    [InlineData("en-US", "The café visit is included.")]
    [InlineData("pt-BR", "A visita ao café está incluída.")]
    public async Task Renders_bounded_tagged_documents_with_embedded_branding_and_a_link(string language, string paragraph)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await ChromiumPdfTestRuntime.Launch(playwright);
        var document = ChromiumPdfTestRuntime.Document(language, paragraph, ChromiumPdfTestRuntime.EmbeddedLogo);
        var renderer = ChromiumPdfTestRuntime.QualifiedRenderer(browser);

        // Act
        var artifact = await renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);

        // Assert
        artifact.Span.StartsWith("%PDF-"u8).ShouldBeTrue();
        browser.Contexts.ShouldBeEmpty();
        await ChromiumPdfTestRuntime.Save(language, artifact, ct);
    }
}
