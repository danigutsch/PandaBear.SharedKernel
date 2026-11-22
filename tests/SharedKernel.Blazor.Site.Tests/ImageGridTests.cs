using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the image grid.</summary>
public sealed class ImageGridTests : BunitContext
{
    [Fact]
    public void Each_image_is_a_figure_with_alt_dimensions_and_an_optional_caption()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, Images(2)));

        // Assert
        var figures = cut.FindAll("figure");
        figures.Count.ShouldBe(2);
        var image = figures[0].QuerySelector("img")!;
        image.GetAttribute("src").ShouldBe("/img1.jpg");
        image.GetAttribute("alt").ShouldBe("Photo 1");
        image.GetAttribute("width").ShouldBe("400");
        image.GetAttribute("height").ShouldBe("300");
        figures[0].QuerySelector("figcaption")!.TextContent.ShouldBe("First");
        figures[1].QuerySelector("figcaption").ShouldBeNull();
    }

    [Fact]
    public void Images_are_lazy_except_the_first_eager_count()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(4))
            .Add(component => component.EagerCount, 2));

        // Assert
        cut.FindAll("img").Select(image => image.GetAttribute("loading")).ShouldBeEquivalentTo("eager", "eager", "lazy", "lazy");
    }

    [Fact]
    public void A_prioritized_first_image_is_eager_with_high_fetch_priority()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(2))
            .Add(component => component.PrioritizeFirstImage, true));

        // Assert
        var images = cut.FindAll("img");
        images[0].GetAttribute("loading").ShouldBe("eager");
        images[0].GetAttribute("fetchpriority").ShouldBe("high");
        images[1].GetAttribute("loading").ShouldBe("lazy");
        images[1].HasAttribute("fetchpriority").ShouldBeFalse();
    }

    [Fact]
    public void Every_image_is_lazy_by_default()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, Images(2)));

        // Assert
        cut.FindAll("img").ShouldAllSatisfy(image => image.GetAttribute("loading").ShouldBe("lazy"));
    }

    [Fact]
    public void A_gallery_entry_without_an_image_fails_to_render()
    {
        // Arrange & Act
        var render = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage(null!)]));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }

    [Fact]
    public void A_responsive_image_keeps_the_eager_count_on_its_img_inside_the_picture()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage(PictureImage()), new GalleryImage(PictureImage())])
            .Add(component => component.EagerCount, 1));

        // Assert
        cut.FindAll("figure > picture").Count.ShouldBe(2);
        cut.FindAll("picture > img").Select(image => image.GetAttribute("loading")).ShouldBeEquivalentTo("eager", "lazy");
    }

    [Fact]
    public void A_decorative_image_has_an_empty_alt()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage(Image("/a.jpg", ImageAlternative.Decorative()))]));

        // Assert
        cut.Find("img").GetAttribute("alt").ShouldBe(string.Empty);
    }

    [Fact]
    public void Negative_eager_count_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(1))
            .Add(component => component.EagerCount, -1));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_grid()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(1))
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var grid = cut.Find(".site-image-grid");
        BunitElementAssertions.HasClass(grid.ClassList, "extra");
        grid.GetAttribute("data-test").ShouldBe("x");
    }

    [Fact]
    public void The_default_layout_is_the_grid()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, Images(2)));

        // Assert
        cut.Find("div").ClassName.ShouldBe("site-image-grid");
    }

    [Fact]
    public void The_mosaic_layout_keeps_every_figure_lazy_and_in_order()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(3))
            .Add(component => component.Layout, ImageGridLayout.Mosaic));

        // Assert
        var grid = cut.Find("div");
        BunitElementAssertions.HasClass(grid.ClassList, "site-image-grid");
        BunitElementAssertions.HasClass(grid.ClassList, "site-image-grid--mosaic");
        cut.FindAll("figure img").Select(image => image.GetAttribute("src")).ShouldBe(["/img1.jpg", "/img2.jpg", "/img3.jpg"]);
        cut.FindAll("img").ShouldAllSatisfy(image => image.GetAttribute("loading").ShouldBe("lazy"));
    }

    [Fact]
    public void An_undefined_layout_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(1))
            .Add(component => component.Layout, (ImageGridLayout)9));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(ImageGrid.Layout));
    }

    [Fact]
    public void The_thumbnail_layout_keeps_every_figure_in_order_with_its_size()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(3))
            .Add(component => component.Layout, ImageGridLayout.Thumbnails));

        // Assert
        cut.Find("div").ClassName.ShouldBe("site-image-grid site-image-grid--thumbnails");
        cut.FindAll("figure img").Select(image => image.GetAttribute("src")).ShouldBe(["/img1.jpg", "/img2.jpg", "/img3.jpg"]);
        cut.FindAll("img").ShouldAllSatisfy(image => image.HasAttribute("width").ShouldBeTrue());
        cut.FindAll("img").ShouldAllSatisfy(image => image.HasAttribute("height").ShouldBeTrue());
    }

    [Fact]
    public void A_linked_image_is_wrapped_in_a_plain_link_named_by_its_alt_text_with_the_caption_outside_it()
    {
        // Arrange
        GalleryImage[] images =
        [
            new(Image("/img1.jpg", ImageAlternative.Describe("A glacier")), "Day 3") { Href = LinkTarget.FromString("/photos/glacier.jpg") },
            new(Image("/img2.jpg", ImageAlternative.Describe("A lake"))),
        ];

        // Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, images));

        // Assert
        var figures = cut.FindAll("figure");
        var link = figures[0].Children[0];
        link.TagName.ShouldBe("A");
        link.ClassName.ShouldBe("site-image-grid__link");
        link.GetAttribute("href").ShouldBe("/photos/glacier.jpg");
        link.HasAttribute("target").ShouldBeFalse();
        link.HasAttribute("aria-label").ShouldBeFalse();
        link.Children.ShouldHaveSingleItem().GetAttribute("alt").ShouldBe("A glacier");
        figures[0].Children[1].TagName.ShouldBe("FIGCAPTION");
        figures[1].Children.ShouldHaveSingleItem().TagName.ShouldBe("IMG");
        cut.FindAll("a").Count.ShouldBe(1);
    }

    [Fact]
    public void A_linked_decorative_image_is_rejected_because_the_link_would_have_no_name()
    {
        // Arrange
        GalleryImage[] images = [new(Image("/img1.jpg", ImageAlternative.Decorative()), "A caption") { Href = LinkTarget.FromString("/img1.jpg") }];

        // Act
        var render = () => Render<ImageGrid>(parameters => parameters.Add(component => component.Images, images));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }

    [Fact]
    public void Thumbnails_tell_the_browser_the_tile_size_instead_of_each_image_s_own_sizes()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage(PictureImage())])
            .Add(component => component.Layout, ImageGridLayout.Thumbnails));

        // Assert
        cut.FindAll("source").ShouldAllSatisfy(source => source.GetAttribute("sizes").ShouldBe(ImageGrid.ThumbnailSizes));
        cut.Find("img").GetAttribute("sizes").ShouldBe(ImageGrid.ThumbnailSizes);
        ImageGrid.ThumbnailSizes.ShouldBe("(min-width: 74rem) 12rem, (min-width: 48rem) 16vw, 9.5rem");
    }

    [Theory]
    [InlineData(ImageGridLayout.Grid)]
    [InlineData(ImageGridLayout.Mosaic)]
    public void Whole_photo_layouts_keep_each_image_s_own_sizes(ImageGridLayout layout)
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage(PictureImage())])
            .Add(component => component.Layout, layout));

        // Assert
        cut.Find("img").GetAttribute("sizes").ShouldBe("(min-width: 48rem) 50vw, 100vw");
    }

    [Theory]
    [InlineData(ImageGridLayout.Grid)]
    [InlineData(ImageGridLayout.Thumbnails)]
    public void Explicit_sizes_replace_the_sizes_of_width_described_images_only(ImageGridLayout layout)
    {
        // Arrange
        GalleryImage[] images = [new(PictureImage()), new(Image("/plain.jpg"))];

        // Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, images)
            .Add(component => component.Layout, layout)
            .Add(component => component.Sizes, "(min-width: 48rem) 25vw, 50vw"));

        // Assert
        var imgs = cut.FindAll("img");
        imgs[0].GetAttribute("sizes").ShouldBe("(min-width: 48rem) 25vw, 50vw");
        cut.FindAll("source").ShouldAllSatisfy(source => source.GetAttribute("sizes").ShouldBe("(min-width: 48rem) 25vw, 50vw"));
        imgs[1].HasAttribute("sizes").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("50vw\n100vw")]
    public void Blank_or_multi_line_sizes_are_rejected(string sizes)
    {
        // Arrange & Act
        var render = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, Images(1))
            .Add(component => component.Sizes, sizes));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Sizes));
    }
}
