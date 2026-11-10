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
    public void Every_image_is_lazy_by_default()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, Images(2)));

        // Assert
        cut.FindAll("img").ShouldAllSatisfy(image => image.GetAttribute("loading").ShouldBe("lazy"));
    }

    [Fact]
    public void An_image_without_an_accessibility_decision_fails_to_render()
    {
        // Arrange & Act
        var render = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage("/a.jpg", null!, 10, 10)]));

        // Assert
        render.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void A_decorative_image_has_an_empty_alt()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage("/a.jpg", ImageAlternative.Decorative(), 10, 10)]));

        // Assert
        cut.Find("img").GetAttribute("alt").ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-1, 10)]
    public void Non_positive_dimensions_are_rejected_because_they_would_not_reserve_space(int width, int height)
    {
        // Arrange & Act
        var render = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage("/a.jpg", ImageAlternative.Describe("x"), width, height)]));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>();
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
}
