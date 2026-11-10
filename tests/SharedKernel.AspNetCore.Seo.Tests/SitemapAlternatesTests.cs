using System.Xml.Linq;

namespace PandaBear.AspNetCore.Seo.Tests;

/// <summary>Tests the hreflang alternate links of sitemap entries.</summary>
public sealed class SitemapAlternatesTests
{
    private static readonly XNamespace Sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    [Fact]
    public async Task Serializes_alternates_including_x_default_as_xhtml_links()
    {
        // Arrange
        SitemapAlternate[] alternates =
        [
            new("pt", new Uri("https://example.com/pt/tours")),
            new("en", new Uri("https://example.com/en/tours")),
            SitemapAlternate.ForDefault(new Uri("https://example.com/pt/tours"))
        ];
        var entry = new SitemapEntry(new Uri("https://example.com/pt/tours"), null, alternates);

        // Act
        await using var stream = await SitemapXmlSerializer.Serialize([entry], TestContext.Current.CancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, TestContext.Current.CancellationToken);

        // Assert
        var url = document.Root.ShouldNotBeNull().Elements(Sitemap + "url").ShouldHaveSingleItem();
        var links = url.Elements(Xhtml + "link").ToArray();
        links.Length.ShouldBe(3);
        links.Select(static link => link.Attribute("rel")!.Value).ShouldBe(["alternate", "alternate", "alternate"]);
        links.Select(static link => link.Attribute("hreflang")!.Value).ShouldBe(["pt", "en", "x-default"]);
        links.Select(static link => link.Attribute("href")!.Value)
            .ShouldBe(["https://example.com/pt/tours", "https://example.com/en/tours", "https://example.com/pt/tours"]);
        document.Root.Attribute(XNamespace.Xmlns + "xhtml")!.Value.ShouldBe(Xhtml.NamespaceName);
    }

    [Fact]
    public async Task Places_alternates_after_loc_and_lastmod()
    {
        // Arrange
        var entry = new SitemapEntry(
            new Uri("https://example.com/pt"),
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            [new SitemapAlternate("en", new Uri("https://example.com/en"))]);

        // Act
        await using var stream = await SitemapXmlSerializer.Serialize([entry], TestContext.Current.CancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, TestContext.Current.CancellationToken);

        // Assert
        var names = document.Root.ShouldNotBeNull().Element(Sitemap + "url")!.Elements().Select(static e => e.Name.LocalName);
        names.ShouldBe(["loc", "lastmod", "link"]);
    }

    [Fact]
    public async Task Escapes_alternate_urls()
    {
        // Arrange
        var entry = new SitemapEntry(
            new Uri("https://example.com/a"),
            null,
            [new SitemapAlternate("en", new Uri("https://example.com/en?a=1&b=<2>"))]);

        // Act
        await using var stream = await SitemapXmlSerializer.Serialize([entry], TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        // Assert
        text.ShouldContain("a=1&amp;b=", StringComparison.Ordinal);
        text.ShouldNotContain("<2>", StringComparison.Ordinal);
        XDocument.Parse(text).Root.ShouldNotBeNull();
    }

    [Fact]
    public async Task Entries_without_alternates_keep_the_original_document_shape()
    {
        // Arrange
        var entry = new SitemapEntry(new Uri("https://example.com/"));

        // Act
        await using var stream = await SitemapXmlSerializer.Serialize([entry], TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        // Assert
        text.ShouldNotContain("xhtml", StringComparison.Ordinal);
        entry.Alternates.ShouldBeEmpty();
    }

    [Fact]
    public void Entry_copies_the_alternates_it_is_given()
    {
        // Arrange
        var source = new List<SitemapAlternate> { new("en", new Uri("https://example.com/en")) };
        var entry = new SitemapEntry(new Uri("https://example.com/pt"), null, source);

        // Act
        source.Clear();

        // Assert
        entry.Alternates.Count.ShouldBe(1);
    }

    [Fact]
    public void Entry_treats_a_null_alternate_collection_as_none_and_rejects_null_members()
    {
        // Arrange
        var location = new Uri("https://example.com/");

        // Act
        var withoutAlternates = new SitemapEntry(location, null, null);
        var nullMember = () => new SitemapEntry(location, null, [null!]);

        // Assert
        withoutAlternates.Alternates.ShouldBeEmpty();
        nullMember.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("pt br")]
    [InlineData("pt\"")]
    [InlineData("-pt")]
    public void Alternate_rejects_invalid_language_tags(string language)
    {
        // Arrange
        var location = new Uri("https://example.com/");

        // Act
        var create = () => new SitemapAlternate(language, location);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("ftp://example.com/file")]
    public void Alternate_rejects_locations_that_are_not_absolute_http_urls(string location)
    {
        // Arrange
        var uri = new Uri(location, UriKind.RelativeOrAbsolute);

        // Act
        var create = () => new SitemapAlternate("en", uri);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Alternate_exposes_its_values_and_a_default_factory()
    {
        // Arrange
        var location = new Uri("https://example.com/");

        // Act
        var alternate = SitemapAlternate.ForDefault(location);

        // Assert
        alternate.Language.ShouldBe(SitemapAlternate.DefaultLanguage);
        alternate.Language.ShouldBe("x-default");
        alternate.Location.ShouldBe(location);
    }
}
