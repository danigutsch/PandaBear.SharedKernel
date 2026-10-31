using System.Text;
using PandaBear.Testing;

namespace PandaBear.DocumentRendering.Tests;

/// <summary>
/// Tests the HTML output of HtmlDocumentRenderer, including escaping, branding and unsafe-value handling.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.DocumentRenderingCapability)]
public sealed class HtmlDocumentRendererTests
{
    [Fact]
    public void Render_escapes_document_content_and_decorates_with_branding()
    {
        // Arrange
        var request = new DocumentRenderRequest("en", "Contract <title>", [new DocumentSection("Terms & conditions", [new DocumentField("Customer <name>", "A & B", DocumentPrivacyClassification.PersonalData)])], new DocumentBrandingSnapshot("B1", "Example <Company>", new Uri("/logo.svg?x=1&y=2", UriKind.Relative)));

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldContain("Contract &lt;title&gt;", StringComparison.Ordinal);
        html.ShouldContain("Terms &amp; conditions", StringComparison.Ordinal);
        html.ShouldContain("Customer &lt;name&gt;", StringComparison.Ordinal);
        html.ShouldContain("A &amp; B", StringComparison.Ordinal);
        html.ShouldContain("alt=\"Example &lt;Company&gt; logo\"", StringComparison.Ordinal);
        html.ShouldContain("src=\"/logo.svg?x=1&amp;y=2\"", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_is_deterministic_and_uses_semantic_print_accessible_html()
    {
        // Arrange
        var request = new DocumentRenderRequest("pt-BR", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], null);
        var renderer = new HtmlDocumentRenderer();

        // Act
        var first = Encoding.UTF8.GetString(renderer.Render(request));
        var second = Encoding.UTF8.GetString(renderer.Render(request));

        // Assert
        first.ShouldBe(second);
        first.ShouldBeWellFormedHtmlDocument();
        first.ShouldContain("<!DOCTYPE html><html lang=\"pt-BR\">", StringComparison.Ordinal);
        first.ShouldContain("<main><h1>", StringComparison.Ordinal);
        first.ShouldContain("<section><h2>", StringComparison.Ordinal);
        first.ShouldContain("<dl><dt>", StringComparison.Ordinal);
        first.ShouldContain("</section></main></body></html>", StringComparison.Ordinal);
        first.ShouldContain("@media print", StringComparison.Ordinal);
        first.ShouldNotContain("<img", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_embeds_a_raster_data_uri_logo()
    {
        // Arrange
        const string logo = "data:image/png;base64,iVBORw0KGgo=";
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], new DocumentBrandingSnapshot("B1", "Example Company", new Uri(logo, UriKind.Absolute)));

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldContain($"<img src=\"{logo}\"", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("data:image/png;base64,/9j/")]
    [InlineData("data:text/html;base64,PGI+")]
    public void Render_excludes_non_raster_or_mismatched_data_uri_logos(string logo)
    {
        // Arrange
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], new DocumentBrandingSnapshot("B1", "Example Company", new Uri(logo, UriKind.Absolute)));

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldNotContain("<img", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_excludes_unsafe_logo_uris()
    {
        // Arrange
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], new DocumentBrandingSnapshot("B1", "Example Company", new Uri("http://example.test/logo.svg", UriKind.Absolute)));

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldNotContain("<img", StringComparison.Ordinal);
        html.ShouldContain("<p>Example Company</p>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_excludes_backslash_logo_paths()
    {
        // Arrange
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], new DocumentBrandingSnapshot("B1", "Example Company", new Uri("/\\evil.test/logo.svg", UriKind.Relative)));

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldNotContain("<img", StringComparison.Ordinal);
        html.ShouldContain("<p>Example Company</p>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_applies_captured_branding_tokens_and_footer()
    {
        // Arrange
        var branding = new DocumentBrandingSnapshot(
            "B1",
            "Example Company",
            new Uri("/logo.svg", UriKind.Relative),
            "#102030",
            "#405060",
            "#fdfdfd",
            "#111111",
            "Montserrat",
            "Inter",
            "Legal footer");
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], branding);

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldBeWellFormedHtmlDocument();
        html.ShouldContain("--document-primary-color:#102030", StringComparison.Ordinal);
        html.ShouldContain("--document-accent-color:#405060", StringComparison.Ordinal);
        html.ShouldContain("--document-background-color:#fdfdfd", StringComparison.Ordinal);
        html.ShouldContain("--document-text-color:#111111", StringComparison.Ordinal);
        html.ShouldContain("--document-heading-font:Montserrat", StringComparison.Ordinal);
        html.ShouldContain("--document-body-font:Inter", StringComparison.Ordinal);
        html.ShouldContain("</main><footer><p>Legal footer</p></footer></body></html>", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_replaces_unsafe_css_color_tokens_with_fallbacks()
    {
        // Arrange
        var branding = new DocumentBrandingSnapshot(
            "B1",
            "Example Company",
            null,
            "red;}body{background:url(https://evil.test/x)",
            "#405060",
            "#fdfdfd",
            "#111111",
            "Montserrat",
            "Inter",
            "Legal footer");
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], branding);

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldContain("--document-primary-color:#000000", StringComparison.Ordinal);
        html.ShouldNotContain("url(", StringComparison.Ordinal);
        html.ShouldNotContain("}body{background", StringComparison.Ordinal);
    }

    [Fact]
    public void Render_replaces_unsafe_css_font_tokens_with_fallbacks()
    {
        // Arrange
        var branding = new DocumentBrandingSnapshot(
            "B1",
            "Example Company",
            null,
            "#102030",
            "#405060",
            "#fdfdfd",
            "#111111",
            "Inter\n}body{background:url(https://evil.test/x)/*",
            "Inter/*comment*/",
            "Legal footer");
        var request = new DocumentRenderRequest("en", "Tour service contract", [new DocumentSection("Travel", [new DocumentField("Dates", "2026-07-11", DocumentPrivacyClassification.Public)])], branding);

        // Act
        var html = Encoding.UTF8.GetString(new HtmlDocumentRenderer().Render(request));

        // Assert
        html.ShouldContain("--document-heading-font:system-ui, sans-serif", StringComparison.Ordinal);
        html.ShouldContain("--document-body-font:system-ui, sans-serif", StringComparison.Ordinal);
        html.ShouldNotContain("url(", StringComparison.Ordinal);
        html.ShouldNotContain("/*", StringComparison.Ordinal);
        html.ShouldNotContain("\n", StringComparison.Ordinal);
    }

}
