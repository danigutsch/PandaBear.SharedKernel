using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>
/// Tests the ordered blocks of a document section, which keep a paragraph that follows a list after it.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class DocumentBlockTests
{
    [Fact]
    public void Render_keeps_a_paragraph_that_follows_a_list_after_the_list()
    {
        // Arrange
        var section = new DocumentSection("Cláusula 17", [])
        {
            Blocks =
            [
                new DocumentParagraph("Antes", DocumentPrivacyClassification.Public),
                new DocumentList(["A", "B"], DocumentPrivacyClassification.Public),
                new DocumentParagraph("Parágrafo único", DocumentPrivacyClassification.Public),
            ],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<p>Antes</p><ul><li>A</li><li>B</li></ul><p>Par&#225;grafo &#250;nico</p></section>", StringComparison.Ordinal);
        html.ShouldNotContain("<dl>", StringComparison.Ordinal);
        html.ShouldBeWellFormedHtmlDocument();
    }

    [Fact]
    public void Render_writes_paragraphs_then_lists_then_blocks_then_fields()
    {
        // Arrange
        var section = new DocumentSection("S", [new DocumentField("K", "V", DocumentPrivacyClassification.Public)])
        {
            Paragraphs = [new DocumentParagraph("P", DocumentPrivacyClassification.Public)],
            Lists = [new DocumentList(["L"], DocumentPrivacyClassification.Public)],
            Blocks =
            [
                new DocumentList(["B"], DocumentPrivacyClassification.Public) { IsOrdered = true },
                new DocumentParagraph("Q", DocumentPrivacyClassification.Public),
            ],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<h2>S</h2><p>P</p><ul><li>L</li></ul><ol><li>B</li></ol><p>Q</p><dl><dt>K</dt><dd>V</dd></dl></section>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_omits_the_empty_field_list_of_a_section_with_only_blocks()
    {
        // Arrange
        var section = new DocumentSection("S", [])
        {
            Blocks = [new DocumentParagraph("Só texto", DocumentPrivacyClassification.Public)],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<h2>S</h2><p>S&#243; texto</p></section>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_applies_the_paragraph_and_list_rules_to_blocks()
    {
        // Arrange
        var unclassifiedParagraph = DocumentRenderingHelper.RenderBlock(new DocumentParagraph("A", DocumentPrivacyClassification.None));
        var blankParagraph = DocumentRenderingHelper.RenderBlock(new DocumentParagraph(" ", DocumentPrivacyClassification.Public));
        var unclassifiedList = DocumentRenderingHelper.RenderBlock(new DocumentList(["A"], DocumentPrivacyClassification.None));
        var emptyList = DocumentRenderingHelper.RenderBlock(new DocumentList([], DocumentPrivacyClassification.Public));
        var nullBlock = DocumentRenderingHelper.RenderBlock(null!);

        // Act & Assert
        unclassifiedParagraph.ShouldThrow<InvalidOperationException>();
        blankParagraph.ShouldThrow<ArgumentException>();
        unclassifiedList.ShouldThrow<InvalidOperationException>();
        emptyList.ShouldThrow<ArgumentOutOfRangeException>();
        nullBlock.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Render_rejects_a_null_block_sequence()
    {
        // Arrange
        var section = new DocumentSection("S", []) { Blocks = null! };
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Act & Assert
        render.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Render_is_deterministic_for_blocks()
    {
        // Arrange
        var request = DocumentRenderingHelper.RequestWith(new DocumentSection("S", [])
        {
            Blocks =
            [
                new DocumentParagraph("A", DocumentPrivacyClassification.PersonalData),
                new DocumentList(["B"], DocumentPrivacyClassification.PersonalData),
            ],
        });
        var renderer = new HtmlDocumentRenderer();

        // Act
        var first = renderer.Render(request);
        var second = renderer.Render(request);

        // Assert
        second.ShouldBe(first);
    }
}
