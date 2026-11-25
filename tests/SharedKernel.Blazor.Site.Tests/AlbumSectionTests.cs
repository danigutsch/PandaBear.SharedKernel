using SharedKernel.BuildingBlocks;
using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the album section: a named section with one page of photos and the pager.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class AlbumSectionTests : BunitContext
{
    [Fact]
    public void The_section_is_named_by_its_heading_whose_ID_comes_from_the_slug()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 6), Images(6));

        // Assert
        var section = cut.Find("section");
        var heading = cut.Find("section h2");
        heading.TextContent.Trim().ShouldBe("Patagônia");
        heading.Id.ShouldBe("album-patagonia");
        section.GetAttribute("aria-labelledby").ShouldBe("album-patagonia");
    }

    [Fact]
    public void The_photos_are_a_grid_whose_first_row_of_four_loads_eagerly_and_the_rest_lazily_with_their_size()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 6), Images(6));

        // Assert
        cut.Find(".site-image-grid").ClassName.ShouldBe("site-image-grid");
        var images = cut.FindAll(".site-image-grid img");
        images.Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "eager", "eager", "eager", "lazy", "lazy"]);
        images.ShouldAllSatisfy(image => image.GetAttribute("width").ShouldBe("400"));
        images.ShouldAllSatisfy(image => image.GetAttribute("height").ShouldBe("300"));
        AlbumSection.DefaultEagerCount.ShouldBe(4);
    }

    [Fact]
    public void Thumbnails_are_opted_into_and_load_their_first_row_of_six_eagerly()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 8, 8), Images(8), parameters => parameters.Add(album => album.Layout, ImageGridLayout.Thumbnails));

        // Assert
        cut.Find(".site-image-grid").ClassName.ShouldBe("site-image-grid site-image-grid--thumbnails");
        cut.FindAll(".site-image-grid img").Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "eager", "eager", "eager", "eager", "eager", "lazy", "lazy"]);
        AlbumSection.DefaultThumbnailEagerCount.ShouldBe(6);
    }

    [Fact]
    public void The_number_of_eager_photos_and_the_layout_can_be_chosen()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 3), Images(3), parameters => parameters
            .Add(album => album.EagerCount, 1)
            .Add(album => album.Layout, ImageGridLayout.Mosaic));

        // Assert
        cut.FindAll("img").Select(image => image.GetAttribute("loading")).ShouldBe(["eager", "lazy", "lazy"]);
        cut.Find(".site-image-grid").ClassList.ShouldContain("site-image-grid--mosaic");
    }

    [Fact]
    public void The_pager_follows_the_photos_and_links_every_page()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(2, 2, 5), Images(2), parameters => parameters.Add(album => album.PageLabelFormat, "Página {0}"));

        // Assert
        var nav = cut.Find("section nav.site-pager");
        nav.GetAttribute("aria-label").ShouldBe("Páginas do álbum");
        cut.Find(".site-image-grid").NextElementSibling.ShouldNotBeNull().TagName.ShouldBe("NAV");
        cut.FindAll("nav a").Select(link => link.GetAttribute("href")).ShouldBe(
            ["/pt/galeria/patagonia", "/pt/galeria/patagonia", "/pt/galeria/patagonia?pagina=2", "/pt/galeria/patagonia?pagina=3", "/pt/galeria/patagonia?pagina=3"]);
        cut.Find("nav a[aria-current=page]").GetAttribute("aria-label").ShouldBe("Página 2");
        cut.Find("a[rel=prev]").TextContent.Trim().ShouldBe("Anterior");
        cut.Find("a[rel=next]").TextContent.Trim().ShouldBe("Próxima");
    }

    [Fact]
    public void A_single_page_has_no_pager()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 6), Images(6));

        // Assert
        cut.FindAll("nav").ShouldBeEmpty();
    }

    [Fact]
    public void The_intro_level_content_and_attributes_are_applied()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 6, 1), Images(1), parameters => parameters
            .Add(album => album.Level, 1)
            .Add(album => album.Intro, "Oito dias no fim do mundo.")
            .Add(album => album.ChildContent, "<p class=\"tour-link\">Ver a viagem</p>")
            .Add(album => album.Class, "album")
            .AddUnmatched("data-album", "patagonia"));

        // Assert
        var section = cut.Find("section");
        section.GetAttribute("data-album").ShouldBe("patagonia");
        section.ClassList.ShouldContain("album");
        cut.Find("h1").Id.ShouldBe("album-patagonia");
        cut.Markup.ShouldContain("Oito dias no fim do mundo.", StringComparison.Ordinal);
        var grid = cut.Find(".site-image-grid");
        grid.NextElementSibling.ShouldNotBeNull().ClassList.ShouldContain("tour-link");
    }

    [Fact]
    public void Missing_or_invalid_parameters_are_rejected()
    {
        // Arrange
        var noSlug = () => Render<AlbumSection>(parameters => parameters
            .Add(album => album.Title, "Patagônia")
            .Add(album => album.Photos, Images(1))
            .Add(album => album.Window, PageWindow.Create(1, 6, 1))
            .Add(album => album.PageHref, _ => Link("/a"))
            .Add(album => album.PagerLabel, "Páginas")
            .Add(album => album.PreviousText, "Anterior")
            .Add(album => album.NextText, "Próxima"));
        var noPhotos = () => this.RenderAlbum(PageWindow.Create(1, 6, 1), null!);
        var negative = () => this.RenderAlbum(PageWindow.Create(1, 6, 1), Images(1), parameters => parameters.Add(album => album.EagerCount, -1));

        // Act & Assert
        noSlug.ShouldThrow<ArgumentNullException>();
        noPhotos.ShouldThrow<ArgumentNullException>();
        negative.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Only_the_heading_of_an_album_in_another_language_carries_lang()
    {
        // Arrange & Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 2, 4), Images(2), parameters => parameters
            .Add(album => album.TitleLanguage, "en")
            .Add(album => album.ChildContent, "<p class=\"tour-link\">Ver a viagem</p>"));

        // Assert
        var marked = cut.FindAll("[lang]");
        marked.Count.ShouldBe(1);
        marked[0].TagName.ShouldBe("H2");
        cut.Find("section").HasAttribute("lang").ShouldBeFalse();
        cut.FindAll("nav.site-pager, img, figcaption, .tour-link").ShouldAllSatisfy(element => element.Closest("[lang]").ShouldBeNull());
    }

    [Fact]
    public void Linked_photos_and_the_sizes_pass_through_to_the_grid()
    {
        // Arrange
        GalleryImage[] photos =
        [
            new(PictureImage()) { Href = LinkTarget.FromString("/media/lake-2400.jpg") },
            new(PictureImage()),
        ];

        // Act
        var defaults = this.RenderAlbum(PageWindow.Create(1, 2, 2), photos, parameters => parameters.Add(album => album.Layout, ImageGridLayout.Thumbnails));
        var chosen = this.RenderAlbum(PageWindow.Create(1, 2, 2), photos, parameters => parameters.Add(album => album.Sizes, "8rem"));

        // Assert
        defaults.FindAll(".site-image-grid__link").Select(link => link.GetAttribute("href")).ShouldBe(["/media/lake-2400.jpg"]);
        defaults.FindAll("img").ShouldAllSatisfy(image => image.GetAttribute("sizes").ShouldBe(ImageGrid.ThumbnailSizes));
        chosen.FindAll("img").ShouldAllSatisfy(image => image.GetAttribute("sizes").ShouldBe("8rem"));
    }
}
