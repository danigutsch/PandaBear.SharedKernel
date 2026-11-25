using SharedKernel.Testing;

namespace SharedKernel.DocumentRendering.Tests;

/// <summary>Tests structured links in rendered document sections.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class DocumentLinkTests
{
    [Fact]
    public void Render_keeps_link_position_and_encodes_its_text_and_target()
    {
        // Arrange
        var section = new DocumentSection("Acesso", [])
        {
            Blocks =
            [
                new DocumentParagraph("Antes", DocumentPrivacyClassification.Public),
                new DocumentLink("Guia <oficial> & serviços",
                    new Uri("https://example.test/guia?lang=pt-BR&view=\"wide\"#detalhes"),
                    DocumentPrivacyClassification.Public),
                new DocumentList(["Informação"], DocumentPrivacyClassification.Public),
            ],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain(
            "<p>Antes</p><p><a href=\"https://example.test/guia?lang=pt-BR&amp;view=&quot;wide&quot;#detalhes\">"
            + "Guia &lt;oficial&gt; &amp; servi&#231;os</a></p><ul><li>Informa&#231;&#227;o</li></ul></section>",
            StringComparison.Ordinal);
        html.ShouldNotContain("<dl>", StringComparison.Ordinal);
        html.ShouldBeWellFormedHtmlDocument();
    }

    [Fact]
    public void Render_rejects_a_link_without_privacy_classification()
    {
        // Arrange
        var render = DocumentRenderingHelper.RenderBlock(new DocumentLink(
            "Guide", new Uri("https://example.test/guide"), DocumentPrivacyClassification.None));

        // Act
        var exception = render.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Document links must be privacy classified before rendering.");
    }

    [Fact]
    public void Render_rejects_a_link_with_blank_text()
    {
        // Arrange
        var render = DocumentRenderingHelper.RenderBlock(new DocumentLink(
            " \t", new Uri("https://example.test/guide"), DocumentPrivacyClassification.Public));

        // Act
        var exception = render.ShouldThrow<ArgumentException>();

        // Assert
        exception.ParamName.ShouldBe(nameof(DocumentLink.Text));
    }

    [Fact]
    public void Render_rejects_a_link_without_a_target()
    {
        // Arrange
        var render = DocumentRenderingHelper.RenderBlock(new DocumentLink(
            "Guide", null!, DocumentPrivacyClassification.Public));

        // Act
        var exception = render.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe(nameof(DocumentLink.Target));
    }

    [Theory]
    [InlineData("/guide")]
    [InlineData("//example.test/guide")]
    [InlineData("http://example.test/guide")]
    [InlineData("mailto:guide@example.test")]
    [InlineData("https://user:placeholder@example.test/guide")]
    [InlineData("https://example.test/a b")]
    public void Render_rejects_an_unsafe_link_target_without_echoing_it(string target)
    {
        // Arrange
        var render = DocumentRenderingHelper.RenderBlock(new DocumentLink(
            "Guide", new Uri(target, UriKind.RelativeOrAbsolute), DocumentPrivacyClassification.Public));

        // Act
        var exception = render.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldStartWith("Document link targets must be safe absolute HTTPS URIs.", StringComparison.Ordinal);
        exception.Message.ShouldNotContain(target, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_rejects_a_link_target_above_the_URI_length_limit()
    {
        // Arrange
        var target = new Uri("https://example.test/" + new string('x', 2048));
        var render = DocumentRenderingHelper.RenderBlock(new DocumentLink(
            "Guide", target, DocumentPrivacyClassification.Public));

        // Act
        var exception = render.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldStartWith("Document link targets must be safe absolute HTTPS URIs.", StringComparison.Ordinal);
    }
}
