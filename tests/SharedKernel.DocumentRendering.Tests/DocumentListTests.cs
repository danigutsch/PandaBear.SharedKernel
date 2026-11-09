using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>
/// Tests the ordered and unordered lists of a document section.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class DocumentListTests
{
    [Fact]
    public void Render_writes_an_unordered_list_by_default()
    {
        // Arrange
        var section = new DocumentSection("Incluído", [])
        {
            Lists = [new DocumentList(["Hotel", "Café da manhã"], DocumentPrivacyClassification.Public)],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<section><h2>Inclu&#237;do</h2><ul><li>Hotel</li><li>Caf&#233; da manh&#227;</li></ul></section>", StringComparison.Ordinal);
        html.ShouldNotContain("<dl>", StringComparison.Ordinal);
        html.ShouldBeWellFormedHtmlDocument();
    }

    [Fact]
    public void Render_writes_an_ordered_list_when_requested()
    {
        // Arrange
        var section = new DocumentSection("Passos", [])
        {
            Lists = [new DocumentList(["Um", "Dois"], DocumentPrivacyClassification.Public) { IsOrdered = true }],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<ol><li>Um</li><li>Dois</li></ol>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_names_a_list_with_an_encoded_accessible_label()
    {
        // Arrange
        var section = new DocumentSection("Serviços", [])
        {
            Lists = [new DocumentList(["A"], DocumentPrivacyClassification.Public) { Label = "Itens \"incluídos\"" }],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<ul aria-label=\"Itens &quot;inclu&#237;dos&quot;\"><li>A</li></ul>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_writes_paragraphs_then_lists_then_fields_in_order()
    {
        // Arrange
        var section = new DocumentSection("S", [new DocumentField("K", "V", DocumentPrivacyClassification.Public)])
        {
            Paragraphs = [new DocumentParagraph("Intro", DocumentPrivacyClassification.Public)],
            Lists =
            [
                new DocumentList(["A"], DocumentPrivacyClassification.Public),
                new DocumentList(["B"], DocumentPrivacyClassification.Public) { IsOrdered = true },
            ],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<h2>S</h2><p>Intro</p><ul><li>A</li></ul><ol><li>B</li></ol><dl><dt>K</dt><dd>V</dd></dl></section>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_encodes_markup_in_list_items()
    {
        // Arrange
        var section = new DocumentSection("S", [])
        {
            Lists = [new DocumentList(["<script>x</script> & \"y\""], DocumentPrivacyClassification.Public)],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<li>&lt;script&gt;x&lt;/script&gt; &amp; &quot;y&quot;</li>", StringComparison.Ordinal);
        html.ShouldNotContain("<script", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Render_omits_the_aria_label_of_a_blank_or_missing_label(string? label)
    {
        // Arrange
        var section = new DocumentSection("S", [])
        {
            Lists = [new DocumentList(["A"], DocumentPrivacyClassification.Public) { Label = label }],
        };

        // Act
        var html = DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(section));

        // Assert
        html.ShouldContain("<ul><li>A</li></ul>", StringComparison.Ordinal);
        html.ShouldNotContain("aria-label", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_is_deterministic_for_lists()
    {
        // Arrange
        var request = DocumentRenderingHelper.RequestWith(new DocumentSection("S", [])
        {
            Lists = [new DocumentList(["A", "B"], DocumentPrivacyClassification.PersonalData)],
        });
        var renderer = new HtmlDocumentRenderer();

        // Act
        var first = renderer.Render(request);
        var second = renderer.Render(request);

        // Assert
        second.ShouldBe(first);
    }

    [Fact]
    public void Render_rejects_a_list_without_privacy_classification()
    {
        // Arrange
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", [])
        {
            Lists = [new DocumentList(["A"], DocumentPrivacyClassification.None)],
        }));

        // Act & Assert
        render.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Render_rejects_missing_list_collections_lists_and_items()
    {
        // Arrange
        var missingCollection = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", []) { Lists = null! }));
        var missingList = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", []) { Lists = [null!] }));
        var missingItems = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", []) { Lists = [new DocumentList(null!, DocumentPrivacyClassification.Public)] }));
        var nullItem = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", []) { Lists = [new DocumentList([null!], DocumentPrivacyClassification.Public)] }));

        // Act & Assert
        missingCollection.ShouldThrow<ArgumentNullException>();
        missingList.ShouldThrow<ArgumentNullException>();
        missingItems.ShouldThrow<ArgumentNullException>();
        nullItem.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Render_rejects_a_blank_list_item(string item)
    {
        // Arrange
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", [])
        {
            Lists = [new DocumentList([item], DocumentPrivacyClassification.Public)],
        }));

        // Act & Assert
        render.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Render_rejects_a_list_without_items()
    {
        // Arrange
        var render = () => DocumentRenderingHelper.RenderHtml(DocumentRenderingHelper.RequestWith(new DocumentSection("S", [])
        {
            Lists = [new DocumentList([], DocumentPrivacyClassification.Public)],
        }));

        // Act & Assert
        render.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
