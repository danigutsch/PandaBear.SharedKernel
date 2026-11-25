using System.Security.Cryptography;
using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>Verifies the UTF-8 output budget and cancellation of bounded HTML.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class BoundedHtmlDocumentRendererTests
{
    [Fact]
    public void An_exact_budget_preserves_escaped_multilingual_HTML_bytes_and_digest()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var text = new string('x', 511) + "🧭 Café & São Paulo <travel> 'quote' \"quote\"";
        var request = new DocumentRenderRequest("pt-BR", "Viagem & serviços",
            [
                new DocumentSection("Condições", [new DocumentField("Nome & data", text, DocumentPrivacyClassification.Public)])
                {
                    Blocks = [new DocumentLink("Informação & horários", new Uri("https://example.invalid/travel?x=1&y=2"), DocumentPrivacyClassification.Public)],
                },
            ],
            new DocumentBrandingSnapshot("1", "Viagem & serviços", new Uri("/logo.png?x=1&y=2", UriKind.Relative),
                "#102030", "#405060", "#ffffff", "#111111", "  Inter ", "Noto Sans", "Obrigado!"));
        var renderer = new HtmlDocumentRenderer();
        var original = renderer.Render(request);

        // Act
        var bounded = HtmlDocumentRenderer.Render(request, original.Length, ct);

        // Assert
        bounded.ShouldBe(original);
        SHA256.HashData(bounded).ShouldBe(SHA256.HashData(original));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Rejects_output_when_the_budget_is_zero_or_one_byte_short(int difference)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = ChromiumPdfTestRuntime.Document("pt-BR", "Café & chá 🧭", logo: null);
        var renderer = new HtmlDocumentRenderer();
        var original = renderer.Render(request);
        var budget = difference == 0 ? 0 : original.Length - difference;
        Action render = () => HtmlDocumentRenderer.Render(request, budget, ct);

        // Act
        var failure = render.ShouldThrow<InvalidDataException>();

        // Assert
        failure.Message.ShouldBe("The document HTML exceeds its byte limit.");
    }

    [Fact]
    public void Rejects_a_negative_budget()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var request = ChromiumPdfTestRuntime.Document("en-US", "Travel terms", logo: null);
        Action render = () => HtmlDocumentRenderer.Render(request, -1, ct);

        // Act
        var failure = render.ShouldThrow<ArgumentOutOfRangeException>();

        // Assert
        failure.ParamName.ShouldBe("maxOutputBytes");
    }

    [Fact]
    public async Task Cancels_before_rendering_or_rejecting_an_empty_budget()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var request = ChromiumPdfTestRuntime.Document("en-US", "Travel terms", logo: null);
        Action render = () => HtmlDocumentRenderer.Render(request, 0, cancellation.Token);

        // Act
        var failure = render.ShouldThrow<OperationCanceledException>();

        // Assert
        failure.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public void Cancels_when_request_traversal_cancels_the_caller()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var section = new DocumentSection("Terms", []);
        var request = new DocumentRenderRequest("en-US", "Travel terms", new CancellingDocumentSections(section, cancellation), null);
        Action render = () => HtmlDocumentRenderer.Render(request, 2_048, cancellation.Token);

        // Act
        var failure = render.ShouldThrow<OperationCanceledException>();

        // Assert
        failure.CancellationToken.ShouldBe(cancellation.Token);
    }
}
