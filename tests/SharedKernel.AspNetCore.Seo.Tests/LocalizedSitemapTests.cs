namespace PandaBear.AspNetCore.Seo.Tests;

/// <summary>Exercises localized groups independently of route discovery.</summary>
public sealed class LocalizedSitemapTests
{
    [Fact]
    public void Published_entries_share_reciprocal_links_and_keep_their_dates()
    {
        // Arrange
        var portuguese = new Uri("https://example.org/pt/oficina");
        var english = new Uri("https://example.org/en/workshop");
        var fallback = new Uri("https://example.org/workshop");
        var modified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var entries = LocalizedSitemap.Build(
            [(new SitemapAlternate("pt-BR", portuguese), modified), (new SitemapAlternate("en", english), null)],
            fallback);

        // Assert
        entries.Select(entry => entry.Location).ShouldBe([portuguese, english]);
        entries.Select(entry => entry.LastModified).ShouldBe([modified, null]);
        foreach (var entry in entries)
        {
            entry.Alternates.Select(alternate => alternate.Location).ShouldBe([portuguese, english, fallback]);
            entry.Alternates.Select(alternate => alternate.Language).ShouldBe(["pt-BR", "en", "x-default"]);
        }
    }

    [Fact]
    public void Missing_translation_does_not_produce_a_URL_or_a_fallback()
    {
        // Arrange
        var english = new SitemapAlternate("en", new Uri("https://example.org/en/workshop"));

        // Act
        var entries = LocalizedSitemap.Build([(english, null)]);

        // Assert
        entries.ShouldHaveSingleItem().Alternates.ShouldHaveSingleItem().ShouldBe(english);
    }
}
