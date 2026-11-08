using System.Xml.Linq;

namespace PandaBear.AspNetCore.Seo.Tests;

/// <summary>Characterizes the sitemap value types and XML serialization that moved into the Seo package.</summary>
public sealed class SitemapXmlSerializerTests
{
    private static readonly XNamespace Sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [Fact]
    public async Task Serializes_locations_and_utc_last_modified_timestamps()
    {
        // Arrange
        SitemapEntry[] entries =
        [
            new(new Uri("https://example.com/"), new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2))),
            new(new Uri("https://example.com/about"))
        ];

        // Act
        await using var stream = await SitemapXmlSerializer.Serialize(entries, TestContext.Current.CancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, TestContext.Current.CancellationToken);

        // Assert
        var urls = document.Root.ShouldNotBeNull().Elements(Sitemap + "url").ToArray();
        urls.Length.ShouldBe(2);
        urls[0].Element(Sitemap + "loc")!.Value.ShouldBe("https://example.com/");
        urls[0].Element(Sitemap + "lastmod")!.Value.ShouldBe("2026-01-02T01:04:05Z");
        urls[1].Element(Sitemap + "loc")!.Value.ShouldBe("https://example.com/about");
        urls[1].Element(Sitemap + "lastmod").ShouldBeNull();
    }

    [Fact]
    public async Task Rejects_more_entries_than_the_sitemap_protocol_allows()
    {
        // Arrange
        var entries = Enumerable.Range(0, SitemapXmlSerializer.MaximumUrlCount + 1)
            .Select(static index => new SitemapEntry(new Uri($"https://example.com/{index}")));

        // Act
        Func<Task> serialize = () => SitemapXmlSerializer.Serialize(entries, TestContext.Current.CancellationToken);

        // Assert
        await serialize.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("ftp://example.com/file")]
    public void Entry_rejects_locations_that_are_not_absolute_http_urls(string location)
    {
        // Arrange
        var uri = new Uri(location, UriKind.RelativeOrAbsolute);

        // Act
        var create = () => new SitemapEntry(uri);

        // Assert
        create.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com/", true)]
    [InlineData("https://example.com/path", false)]
    [InlineData("https://example.com/?q=1", false)]
    [InlineData("https://user@example.com/", false)]
    [InlineData("ftp://example.com/", false)]
    [InlineData(" https://example.com/", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Canonical_origin_accepts_only_bare_http_and_https_origins(string? value, bool expected)
    {
        // Act
        var valid = SitemapCanonicalOrigin.IsValid(value);

        // Assert
        valid.ShouldBe(expected);
    }

    [Fact]
    public void Canonical_origin_parse_rejects_invalid_values()
    {
        // Act
        var parse = () => SitemapCanonicalOrigin.Parse("https://example.com/path");

        // Assert
        parse.ShouldThrow<ArgumentException>();
    }
}
