using PandaBear.BuildingBlocks;
using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests which album images load eagerly, and the album edge cases of the review.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class AlbumLoadingTests : BunitContext
{
    [Fact]
    public void A_card_loads_its_image_eagerly_only_when_asked()
    {
        // Arrange & Act
        var lazy = Render<Card>(parameters => parameters.Add(card => card.Title, "A").Add(card => card.Image, Image("/a.jpg")));
        var eager = Render<Card>(parameters => parameters.Add(card => card.Title, "A").Add(card => card.Image, Image("/a.jpg")).Add(card => card.EagerImage, true));

        // Assert
        lazy.Find("img").GetAttribute("loading").ShouldBe("lazy");
        eager.Find("img").GetAttribute("loading").ShouldBe("eager");
    }

    [Fact]
    public void The_covers_of_the_first_row_of_the_index_load_eagerly()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, AlbumRendering.Summaries(6)));

        // Assert
        cut.FindAll("img").Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "eager", "eager", "eager", "lazy", "lazy"]);
        AlbumIndex.DefaultEagerCount.ShouldBe(4);
    }

    [Fact]
    public void The_number_of_eager_covers_can_be_chosen()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, AlbumRendering.Summaries(3)).Add(index => index.EagerCount, 0));

        // Assert
        cut.FindAll("img").ShouldAllSatisfy(image => image.GetAttribute("loading").ShouldBe("lazy"));
    }

    [Fact]
    public void An_album_without_a_cover_has_no_media_box()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, [new AlbumSummary("Andes", Link("/andes"))]));

        // Assert
        cut.FindAll(".site-card__media, img").ShouldBeEmpty();
    }

    [Fact]
    public void The_index_rejects_a_missing_address_or_a_negative_eager_count()
    {
        // Arrange
        var noHref = () => Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, [new AlbumSummary("Andes", null!)]));
        var negative = () => Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, AlbumRendering.Summaries(1)).Add(index => index.EagerCount, -1));

        // Act & Assert
        noHref.ShouldThrow<ArgumentNullException>();
        negative.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_mosaic_loads_only_its_first_photo_eagerly_by_default_because_its_columns_run_down_the_page()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 6), Images(6), parameters => parameters.Add(album => album.Layout, ImageGridLayout.Mosaic));

        // Assert
        cut.FindAll("img").Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "lazy", "lazy", "lazy", "lazy", "lazy"]);
        AlbumSection.DefaultMosaicEagerCount.ShouldBe(1);
    }

    [Fact]
    public void An_explicit_eager_count_also_applies_to_a_mosaic()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 4), Images(4), parameters => parameters
            .Add(album => album.Layout, ImageGridLayout.Mosaic)
            .Add(album => album.EagerCount, 3));

        // Assert
        cut.FindAll("img").Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "eager", "eager", "lazy"]);
    }

    [Fact]
    public void The_first_photo_can_be_the_page_s_main_image()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 3), Images(3), parameters => parameters
            .Add(album => album.PrioritizeFirstImage, true)
            .Add(album => album.EagerCount, 0));

        // Assert
        var images = cut.FindAll("img");
        images[0].GetAttribute("fetchpriority").ShouldBe("high");
        images[0].GetAttribute("loading").ShouldBe("eager");
        images[1].HasAttribute("fetchpriority").ShouldBeFalse();
    }

    [Fact]
    public void The_surrounding_pages_reach_the_pager()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(10, 1, 20), Images(1), parameters => parameters.Add(album => album.SurroundingPages, 0));

        // Assert
        cut.FindAll("nav li").Select(item => item.TextContent.Trim()).ShouldBe(["Anterior", "1", "…", "10", "…", "20", "Próxima"]);
    }

    [Fact]
    public void An_album_without_photos_renders_its_section_without_a_grid_item_or_a_pager()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 0), []);

        // Assert
        cut.Find("section h2").Id.ShouldBe("album-patagonia");
        cut.FindAll("img, figure, nav").ShouldBeEmpty();
    }

    [Fact]
    public void A_clamped_window_marks_the_last_page_as_current()
    {
        // Arrange
        var window = PageWindow.Create(9, 6, 12);

        // Act
        var cut = this.RenderAlbum(window, Images(6));

        // Assert
        window.IsOutOfRange.ShouldBeTrue();
        cut.Find("nav a[aria-current=page]").TextContent.Trim().ShouldBe("2");
        cut.FindAll("a[rel=next]").ShouldBeEmpty();
    }
}
