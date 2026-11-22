using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the album index, a card grid of albums.</summary>
public sealed class AlbumIndexTests : BunitContext
{
    private static readonly AlbumSummary[] Albums =
    [
        new("Patagônia", Link("/pt/galeria/patagonia"), Image("/c1.jpg", ImageAlternative.Decorative()), "12 fotos"),
        new("Andes", Link("/pt/galeria/andes"), PhotoCountText: "1 foto", Culture: Culture("en")),
    ];

    [Fact]
    public void Each_album_is_a_card_whose_heading_links_to_the_album()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, Albums));

        // Assert
        var grid = cut.Find("div");
        grid.ClassList.ShouldContain("site-card-grid");
        grid.ClassList.ShouldContain("site-album-index");
        var cards = cut.FindAll("article.site-card");
        cards.Count.ShouldBe(2);
        var links = cut.FindAll("article h3 a");
        links.Select(link => link.TextContent).ShouldBe(["Patagônia", "Andes"]);
        links.Select(link => link.GetAttribute("href")).ShouldBe(["/pt/galeria/patagonia", "/pt/galeria/andes"]);
    }

    [Fact]
    public void A_cover_loads_lazily_with_its_size_and_the_count_text_is_the_application_copy()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, Albums));

        // Assert
        var cover = cut.Find("article img");
        cover.GetAttribute("loading").ShouldBe("lazy");
        cover.GetAttribute("width").ShouldBe("400");
        cover.GetAttribute("height").ShouldBe("300");
        cut.FindAll("img").Count.ShouldBe(1);
        cut.FindAll(".site-album-index__count").Select(count => count.TextContent).ShouldBe(["12 fotos", "1 foto"]);
    }

    [Fact]
    public void An_album_in_another_language_is_marked_with_lang()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, Albums));

        // Assert
        var cards = cut.FindAll("article");
        cards[0].HasAttribute("lang").ShouldBeFalse();
        cards[1].GetAttribute("lang").ShouldBe("en");
    }

    [Fact]
    public void The_heading_level_and_extra_attributes_are_applied()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters
            .Add(index => index.Albums, Albums)
            .Add(index => index.HeadingLevel, 2)
            .Add(index => index.Class, "my-albums")
            .AddUnmatched("aria-label", "Albums"));

        // Assert
        cut.FindAll("article h2 a").Count.ShouldBe(2);
        var grid = cut.Find("div.site-album-index");
        grid.ClassList.ShouldContain("my-albums");
        grid.GetAttribute("aria-label").ShouldBe("Albums");
    }

    [Fact]
    public void Nothing_is_rendered_without_albums()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, []));

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Missing_albums_or_a_missing_album_are_rejected()
    {
        // Arrange
        var noAlbums = () => Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, null!));
        var nullAlbum = () => Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, [null!]));

        // Act & Assert
        noAlbums.ShouldThrow<ArgumentNullException>();
        nullAlbum.ShouldThrow<ArgumentNullException>();
    }
}
