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
            .Add(component => component.ImageSrc, "/a.jpg"));

        // Assert
        render.ShouldThrow<InvalidOperationException>().Message.ShouldContain("ImageAlt", StringComparison.Ordinal);
    }

    [Fact]
    public void Card_with_an_image_and_no_accessibility_decision_fails_to_render()
    {
        // Arrange & Act
        var render = () => Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, "/a.jpg"));

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
            .Add(component => component.ImageSrc, "/a.jpg")
            .Add(component => component.ImageAlt, ImageAlternative.Decorative()));

        // Assert
        cut.Find("img").GetAttribute("alt").ShouldBe(string.Empty);
    }

    [Fact]
    public void Non_positive_dimensions_are_rejected_because_they_would_not_reserve_space()
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.ImageSrc, "/a.jpg")
            .Add(component => component.ImageAlt, ImageAlternative.Describe("x"))
            .Add(component => component.ImageWidth, 0));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
