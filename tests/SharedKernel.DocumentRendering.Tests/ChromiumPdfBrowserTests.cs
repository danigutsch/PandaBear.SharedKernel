using System.ComponentModel;
using PandaBear.Testing;
using PandaBear.Testing.Playwright;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Exercises the renderer's failure boundaries using the shared test browser.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class ChromiumPdfBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private readonly string _unavailableQpdf = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "qpdf");

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData("/branding/logo.png")]
    [InlineData("https://example.invalid/logo.png")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    public async Task Refuses_non_embedded_or_unreadable_branding_without_returning_a_PDF(string logo)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var document = ChromiumPdfTestRuntime.Document("en-US", "Synthetic travel terms.", new Uri(logo, UriKind.RelativeOrAbsolute));
        var renderer = ChromiumPdfTestRuntime.Renderer(chromium.Browser, _unavailableQpdf);

        // Act
        Func<Task> render = () => renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);
        var failure = await render.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldContain("logo", StringComparison.Ordinal);
        chromium.Browser.Contexts.ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Refuses_an_oversized_PDF_and_closes_the_private_context()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var document = ChromiumPdfTestRuntime.Document("en-US", "The café visit is included.", logo: null);
        var renderer = ChromiumPdfTestRuntime.Renderer(chromium.Browser, _unavailableQpdf, maxPdfBytes: 1_024);

        // Act
        Func<Task> render = () => renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);
        var failure = await render.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldContain("byte limit", StringComparison.Ordinal);
        chromium.Browser.Contexts.ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Refuses_oversized_HTML_before_processing_later_document_fields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var document = new DocumentRenderRequest("en-US", "Synthetic travel terms",
            [
                new DocumentSection("Contract details", [new DocumentField("Later field", "Unclassified", DocumentPrivacyClassification.None)])
                {
                    Paragraphs = [new DocumentParagraph(new string('&', 20_000), DocumentPrivacyClassification.Public)],
                },
            ],
            null);
        var renderer = ChromiumPdfTestRuntime.Renderer(chromium.Browser, _unavailableQpdf);

        // Act
        Func<Task> render = () => renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);
        var failure = await render.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe("The document HTML exceeds its byte limit.");
        chromium.Browser.Contexts.ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Closes_the_private_context_before_reporting_an_unavailable_normalizer()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var document = ChromiumPdfTestRuntime.Document("en-US", "The café visit is included.", ChromiumPdfTestRuntime.EmbeddedLogo);
        var renderer = ChromiumPdfTestRuntime.Renderer(chromium.Browser, _unavailableQpdf);

        // Act
        Func<Task> render = () => renderer.Render(document, ChromiumPdfTestRuntime.DocumentDate, ct);
        var failure = await render.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.InnerException.ShouldBeOfType<Win32Exception>();
        failure.Message.ShouldContain(_unavailableQpdf, StringComparison.Ordinal);
        chromium.Browser.Contexts.ShouldBeEmpty();
    }
}
