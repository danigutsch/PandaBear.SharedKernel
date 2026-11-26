using SharedKernel.BuildingBlocks;
using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the image accessibility decision and the safety and layout rules every image source must meet.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteImageRulesTests : BunitContext
{
    [Fact]
    public void An_image_without_an_accessibility_decision_cannot_be_created()
    {
        // Arrange & Act
        var create = () => ResponsiveImage.Create(Link("/a.jpg"), 10, 10, null!);

        // Assert
        create.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("alt");
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
    public void A_stored_alt_text_becomes_the_same_decision()
    {
        // Arrange
        var stored = ImageAltText.Create(" A lake ", decorative: false).ShouldBeSuccess();
        var decorativeStored = ImageAltText.Create(null, decorative: true).ShouldBeSuccess();

        // Act
        var described = ImageAlternative.From(stored);
        var decorative = ImageAlternative.From(decorativeStored);
        var missing = () => ImageAlternative.From(null!);

        // Assert
        described.ShouldBe(ImageAlternative.Describe("A lake"));
        decorative.ShouldBe(ImageAlternative.Decorative());
        missing.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe("alt");
    }

    [Fact]
    public void Decorative_image_renders_an_empty_alt()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Heading, "Title")
            .Add(component => component.Image, Image("/a.jpg", ImageAlternative.Decorative())));

        // Assert
        cut.Find("img").GetAttribute("alt").ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0, 10, "width")]
    [InlineData(10, -1, "height")]
    public void Non_positive_dimensions_are_rejected_because_they_would_not_reserve_space(int width, int height, string parameter)
    {
        // Arrange & Act
        var create = () => ResponsiveImage.Create(Link("/a.jpg"), width, height, ImageAlternative.Describe("x"));

        // Assert
        create.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Components_without_an_image_render_none()
    {
        // Arrange & Act
        var card = Render<Card>(parameters => parameters.Add(component => component.Heading, "Title"));
        var hero = Render<Hero>(parameters => parameters.Add(component => component.Heading, "Title"));

        // Assert
        card.FindAll("img").ShouldBeEmpty();
        hero.FindAll("img").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("mailto:a@example.org")]
    [InlineData("tel:+5511999990000")]
    [InlineData("http://example.org/a.jpg")]
    [InlineData("a.jpg")]
    public void Image_addresses_that_are_not_root_relative_or_https_are_rejected_everywhere(string src)
    {
        // Arrange
        var unsafeTarget = Link(src);
        var alt = ImageAlternative.Decorative();

        // Act
        var fallback = () => ResponsiveImage.Create(unsafeTarget, 10, 10, alt);
        var widthCandidate = () => ImageCandidate.AtWidth(unsafeTarget, 10);
        var densityCandidate = () => ImageCandidate.AtDensity(unsafeTarget, 2m);

        // Assert
        fallback.ShouldThrow<ArgumentException>().ParamName.ShouldBe("src");
        widthCandidate.ShouldThrow<ArgumentException>().ParamName.ShouldBe("src");
        densityCandidate.ShouldThrow<ArgumentException>().ParamName.ShouldBe("src");
    }
}
