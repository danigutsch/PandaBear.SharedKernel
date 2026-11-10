namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the hero.</summary>
public sealed class HeroTests : BunitContext
{
    [Fact]
    public void Hero_has_one_level_one_heading_subtitle_and_actions()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Subtitle, "Small groups")
            .Add(component => component.Actions, "<a id=\"cta\" href=\"/tours\">Tours</a>"));

        // Assert
        cut.FindAll("h1").Count.ShouldBe(1);
        cut.Find("h1").TextContent.ShouldBe("Discover");
        cut.Find(".site-hero__subtitle").TextContent.ShouldBe("Small groups");
        cut.Find(".site-hero__actions #cta").ShouldNotBeNull();
        cut.FindAll("img").ShouldBeEmpty();
    }

    [Fact]
    public void Image_is_rendered_eagerly_with_its_alt_and_dimensions()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.ImageSrc, "/hero.jpg")
            .Add(component => component.ImageAlt, ImageAlternative.Describe("A lake"))
            .Add(component => component.ImageWidth, 1600)
            .Add(component => component.ImageHeight, 900));

        // Assert
        var image = cut.Find("img");
        image.GetAttribute("src").ShouldBe("/hero.jpg");
        image.GetAttribute("alt").ShouldBe("A lake");
        image.GetAttribute("width").ShouldBe("1600");
        image.GetAttribute("height").ShouldBe("900");
        image.GetAttribute("loading").ShouldBe("eager");
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_root()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var root = cut.Find(".site-hero");
        BunitElementAssertions.HasClass(root.ClassList, "extra");
        root.GetAttribute("data-test").ShouldBe("x");
    }
}
