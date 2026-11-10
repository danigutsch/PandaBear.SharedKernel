namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the image accessibility decision and the components that require it.</summary>
public sealed class SiteImageRulesTests : BunitContext
{
    [Fact]
    public void Hero_with_an_image_and_no_accessibility_decision_fails_to_render()
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, LinkTarget.FromString("/a.jpg")));

        // Assert
        render.ShouldThrow<InvalidOperationException>().Message.ShouldContain("ImageAlt", StringComparison.Ordinal);
    }

    [Fact]
    public void Card_with_an_image_and_no_accessibility_decision_fails_to_render()
    {
        // Arrange & Act
        var render = () => Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, LinkTarget.FromString("/a.jpg")));

        // Assert
        render.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_description_is_not_a_decision(string text)
    {
        // Arrange & Act
        var describe = () => ImageAlternative.Describe(text);

        // Assert
        describe.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Decorative_and_described_alternatives_carry_their_text()
    {
        // Arrange & Act
        var decorative = ImageAlternative.Decorative();
        var described = ImageAlternative.Describe(" A lake ");

        // Assert
        decorative.IsDecorative.ShouldBeTrue();
        decorative.Text.ShouldBe(string.Empty);
        described.IsDecorative.ShouldBeFalse();
        described.Text.ShouldBe("A lake");
        described.ShouldBe(ImageAlternative.Describe("A lake"));
    }

    [Fact]
    public void Decorative_image_renders_an_empty_alt()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, LinkTarget.FromString("/a.jpg"))
            .Add(component => component.ImageAlt, ImageAlternative.Decorative())
            .Add(component => component.ImageWidth, 10)
            .Add(component => component.ImageHeight, 10));

        // Assert
        cut.Find("img").GetAttribute("alt").ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0, 10, nameof(Hero.ImageWidth))]
    [InlineData(10, -1, nameof(Hero.ImageHeight))]
    public void Non_positive_dimensions_are_rejected_because_they_would_not_reserve_space(int width, int height, string parameter)
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, LinkTarget.FromString("/a.jpg"))
            .Add(component => component.ImageAlt, ImageAlternative.Describe("x"))
            .Add(component => component.ImageWidth, width)
            .Add(component => component.ImageHeight, height));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(parameter);
    }

    [Theory]
    [InlineData(null, 10, nameof(Hero.ImageWidth))]
    [InlineData(10, null, nameof(Hero.ImageHeight))]
    [InlineData(null, null, nameof(Hero.ImageWidth))]
    public void An_image_without_width_or_height_is_rejected_because_it_would_shift_the_layout(int? width, int? height, string parameter)
    {
        // Arrange & Act
        var hero = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, LinkTarget.FromString("/a.jpg"))
            .Add(component => component.ImageAlt, ImageAlternative.Describe("x"))
            .Add(component => component.ImageWidth, width)
            .Add(component => component.ImageHeight, height));
        var card = () => Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, LinkTarget.FromString("/a.jpg"))
            .Add(component => component.ImageAlt, ImageAlternative.Describe("x"))
            .Add(component => component.ImageWidth, width)
            .Add(component => component.ImageHeight, height));

        // Assert
        hero.ShouldThrow<ArgumentException>().ParamName.ShouldBe(parameter);
        card.ShouldThrow<ArgumentException>().ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Dimensions_without_an_image_are_ignored()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageWidth, 10));

        // Assert
        cut.FindAll("img").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("mailto:a@example.org")]
    [InlineData("tel:+5511999990000")]
    [InlineData("http://example.org/a.jpg")]
    [InlineData("a.jpg")]
    public void Image_sources_that_are_not_root_relative_or_https_are_rejected(string src)
    {
        // Arrange
        var source = LinkTarget.FromString(src);

        // Act
        var hero = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, source)
            .Add(component => component.ImageAlt, ImageAlternative.Decorative())
            .Add(component => component.ImageWidth, 10)
            .Add(component => component.ImageHeight, 10));
        var card = () => Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, source)
            .Add(component => component.ImageAlt, ImageAlternative.Decorative())
            .Add(component => component.ImageWidth, 10)
            .Add(component => component.ImageHeight, 10));
        var grid = () => Render<ImageGrid>(parameters => parameters
            .Add(component => component.Images, [new GalleryImage(source, ImageAlternative.Decorative(), 10, 10)]));

        // Assert
        hero.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Hero.ImageSrc));
        card.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Card.ImageSrc));
        grid.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }
}
