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
}
