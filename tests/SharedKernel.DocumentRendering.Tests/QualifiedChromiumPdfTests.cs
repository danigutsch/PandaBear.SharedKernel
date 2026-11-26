using System.Text.Json;
using Microsoft.Playwright;
using SharedKernel.Testing;

namespace SharedKernel.DocumentRendering.Tests;

/// <summary>Qualifies the optional renderer with the caller-provisioned Chromium and qpdf runtimes.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
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

    [Fact(Explicit = true)]
    public async Task Renders_a_multipage_typed_document_with_default_printing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await ChromiumPdfTestRuntime.Launch(playwright);
        var document = ChromiumPdfTestRuntime.MultipageDocument();
        var renderer = ChromiumPdfTestRuntime.QualifiedRenderer(browser);

        // Act
        var artifact = await renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);
        var repeat = await renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);

        // Assert
        artifact.Span.StartsWith("%PDF-"u8).ShouldBeTrue();
        artifact.Span.SequenceEqual(repeat.Span).ShouldBeTrue();
        browser.Contexts.ShouldBeEmpty();
        await ChromiumPdfTestRuntime.Save("pt-BR-multipage-default", artifact, ct);
    }

    [Fact(Explicit = true)]
    public async Task Renders_deterministic_page_numbers_as_pagination_artifacts()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await ChromiumPdfTestRuntime.Launch(playwright);
        var document = ChromiumPdfTestRuntime.MultipageDocument();
        var renderer = ChromiumPdfTestRuntime.QualifiedRenderer(browser);
        var date = ChromiumPdfTestRuntime.DocumentDate;

        // Act
        var artifact = await renderer.Render(document, date, includePageNumbers: true, ct);
        var contextsAfterFirstRender = browser.Contexts.Count;
        var repeat = await renderer.Render(document, date, includePageNumbers: true, ct);

        // Assert
        artifact.Span.StartsWith("%PDF-"u8).ShouldBeTrue();
        artifact.Span.SequenceEqual(repeat.Span).ShouldBeTrue();
        contextsAfterFirstRender.ShouldBe(0);
        browser.Contexts.ShouldBeEmpty();
        var path = await ChromiumPdfTestRuntime.Save("pt-BR-multipage-numbered", artifact, ct);
        var metadata = await ChromiumPdfTestRuntime.Inspect(path, ["--json=2", "--json-key=pages"], ct);
        using var parsed = JsonDocument.Parse(metadata);
        var pages = parsed.RootElement.GetProperty("pages");
        pages.GetArrayLength().ShouldBeGreaterThan(1);
        foreach (var page in pages.EnumerateArray())
        {
            var pageNumber = page.GetProperty("pageposfrom1").GetInt32();
            var contents = page.GetProperty("contents");
            contents.GetArrayLength().ShouldBeGreaterThan(1, $"Page {pageNumber} needs a separate pagination stream.");
            var reference = contents[0].GetString().ShouldNotBeNull();
            var objectSelector = reference[..^2].Replace(' ', ',');
            var stream = await ChromiumPdfTestRuntime.Inspect(path,
                [$"--show-object={objectSelector}", "--filtered-stream-data"], ct);
            stream.Span.StartsWith("q\n/Artifact << /Type /Pagination /Attached [/Bottom] >> BDC\n"u8)
                .ShouldBeTrue($"Page {pageNumber} should begin with the pagination artifact.");
        }
    }

}
