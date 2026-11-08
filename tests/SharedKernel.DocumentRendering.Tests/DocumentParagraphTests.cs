using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>
/// Tests the prose paragraphs of a document section and the compatibility of sections without paragraphs.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class DocumentParagraphTests
{
    private const string ExistingStyle = "@media print{header{break-inside:avoid}section{break-inside:avoid}}body{font-family:var(--document-body-font,system-ui,sans-serif);background:var(--document-background-color,#fff);color:var(--document-text-color,#111);line-height:1.5;margin:2rem}h1,h2{font-family:var(--document-heading-font,var(--document-body-font,system-ui,sans-serif));color:var(--document-primary-color,currentColor)}header{border-bottom:.25rem solid var(--document-accent-color,currentColor);margin-bottom:1rem;padding-bottom:.5rem}footer{border-top:1px solid var(--document-accent-color,currentColor);margin-top:2rem;padding-top:.5rem}dl{display:grid;grid-template-columns:max-content 1fr;gap:.25rem 1rem}dt{font-weight:700}";

    [Fact]
    public void Render_of_a_section_without_paragraphs_is_unchanged()
    {
        // Arrange
        var request = new DocumentRenderRequest("en", "Receipt", [new DocumentSection("Items", [new DocumentField("Product", "Guide", DocumentPrivacyClassification.Public)])], null);
        var expected = "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" /><title>Receipt</title><style>"
            + ExistingStyle
            + "</style></head><body><main><h1>Receipt</h1><section><h2>Items</h2><dl><dt>Product</dt><dd>Guide</dd></dl></section></main></body></html>";

        // Act
        var html = DocumentRenderingHelper.RenderHtml(request);

        // Assert
        html.ShouldBe(expected);
    }

    [Fact]
    public void Render_of_a_section_with_an_empty_paragraph_list_equals_the_section_without_paragraphs()
    {
        // Arrange
        var fields = new[] { new DocumentField("Product", "Guide", DocumentPrivacyClassification.Public) };
        var plain = DocumentRenderingHelper.RequestWith(new DocumentSection("Items", fields));
        var withEmptyList = DocumentRenderingHelper.RequestWith(new DocumentSection("Items", fields) { Paragraphs = [] });

        // Act
        var plainHtml = DocumentRenderingHelper.RenderHtml(plain);
        var emptyListHtml = DocumentRenderingHelper.RenderHtml(withEmptyList);

        // Assert
        emptyListHtml.ShouldBe(plainHtml);
    }

    [Fact]
    public void Render_writes_paragraphs_in_order_before_the_fields()
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [new DocumentField("Valor", "R$ 100,00", DocumentPrivacyClassification.Operational)])
        {
            Paragraphs =
            [
                new DocumentParagraph("Primeira", DocumentPrivacyClassification.Public),
                new DocumentParagraph("Segunda", DocumentPrivacyClassification.Public),
            ],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<section><h2>Cl&#225;usulas</h2><p>Primeira</p><p>Segunda</p><dl><dt>Valor</dt><dd>R$ 100,00</dd></dl></section>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_omits_the_empty_field_list_of_a_prose_only_section()
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [])
        {
            Paragraphs = [new DocumentParagraph("Texto", DocumentPrivacyClassification.Public)],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<section><h2>Cl&#225;usulas</h2><p>Texto</p></section>", StringComparison.Ordinal);
        html.ShouldNotContain("<dl>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_encodes_markup_and_writes_portuguese_accents_as_character_references()
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [])
        {
            Paragraphs = [new DocumentParagraph("Cláusula 1ª: pagamento até 45 dias <b>\"A\" & 'B'</b>", DocumentPrivacyClassification.Public)],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<p>Cl&#225;usula 1&#170;: pagamento at&#233; 45 dias &lt;b&gt;&quot;A&quot; &amp; &#39;B&#39;&lt;/b&gt;</p>", StringComparison.Ordinal);
        html.ShouldContain("<meta charset=\"utf-8\" />", StringComparison.Ordinal);
        html.ShouldNotContain("<script", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_is_deterministic_for_paragraphs()
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [])
        {
            Paragraphs = [new DocumentParagraph("Texto", DocumentPrivacyClassification.PersonalData)],
        };
        var request = DocumentRenderingHelper.RequestWith(section);
        var renderer = new HtmlDocumentRenderer();

        // Act
        var first = renderer.Render(request);
        var second = renderer.Render(request);

        // Assert
        second.ShouldBe(first);
    }

    [Fact]
    public void Render_rejects_a_paragraph_without_privacy_classification()
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [])
        {
            Paragraphs = [new DocumentParagraph("Texto", DocumentPrivacyClassification.None)],
        };
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Act & Assert
        render.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Render_rejects_a_missing_paragraph_list_and_a_missing_paragraph()
    {
        // Arrange
        var missingList = new DocumentSection("Cláusulas", []) { Paragraphs = null! };
        var missingParagraph = new DocumentSection("Cláusulas", []) { Paragraphs = [null!] };
        var renderList = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(missingList));
        var renderParagraph = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(missingParagraph));

        // Act & Assert
        renderList.ShouldThrow<ArgumentNullException>();
        renderParagraph.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Render_rejects_a_blank_paragraph_text(string text)
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [])
        {
            Paragraphs = [new DocumentParagraph(text, DocumentPrivacyClassification.Public)],
        };
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Act & Assert
        render.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Render_rejects_a_null_paragraph_text()
    {
        // Arrange
        var section = new DocumentSection("Cláusulas", [])
        {
            Paragraphs = [new DocumentParagraph(null!, DocumentPrivacyClassification.Public)],
        };
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Act & Assert
        render.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Section_deconstructs_and_compares_with_its_paragraphs()
    {
        // Arrange
        var fields = new[] { new DocumentField("A", "B", DocumentPrivacyClassification.Public) };
        var paragraphs = new[] { new DocumentParagraph("Texto", DocumentPrivacyClassification.Public) };

        // Act
        var (heading, deconstructedFields) = new DocumentSection("H", fields) { Paragraphs = paragraphs };

        // Assert
        heading.ShouldBe("H");
        deconstructedFields.ShouldBe(fields);
        new DocumentSection("H", fields).Paragraphs.ShouldBeEmpty();
    }
}
