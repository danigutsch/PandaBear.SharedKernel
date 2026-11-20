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
    public void Eyebrow_comes_before_the_heading_and_subtitle_after_it_in_one_hgroup()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Eyebrow, "Since 2004")
            .Add(component => component.Title, "Discover")
            .Add(component => component.Subtitle, "Small groups"));

        // Assert
        var heading = cut.Find(".site-hero__text > hgroup.site-hero__heading");
        heading.Children.Select(child => child.TagName).ShouldBe(["P", "H1", "P"]);
        heading.Children[0].ClassName.ShouldBe("site-hero__eyebrow");
        heading.Children[0].TextContent.ShouldBe("Since 2004");
        heading.Children[1].ClassName.ShouldBe("site-hero__title");
        heading.Children[1].TextContent.ShouldBe("Discover");
        heading.Children[2].ClassName.ShouldBe("site-hero__subtitle");
        heading.Children[2].TextContent.ShouldBe("Small groups");
        cut.FindAll("h1").Count.ShouldBe(1);
    }

    [Fact]
    public void Eyebrow_alone_is_grouped_before_the_heading()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Eyebrow, "Since 2004")
            .Add(component => component.Title, "Discover"));

        // Assert
        var heading = cut.Find("hgroup.site-hero__heading");
        heading.Children.Select(child => child.ClassName).ShouldBe(["site-hero__eyebrow", "site-hero__title"]);
        cut.FindAll(".site-hero__subtitle").ShouldBeEmpty();
    }

    [Fact]
    public void Subtitle_alone_is_grouped_after_the_heading()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Subtitle, "Small groups"));

        // Assert
        var heading = cut.Find("hgroup.site-hero__heading");
        heading.Children.Select(child => child.ClassName).ShouldBe(["site-hero__title", "site-hero__subtitle"]);
        cut.FindAll(".site-hero__eyebrow").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" ", "\t")]
    public void Without_an_eyebrow_or_subtitle_the_heading_stands_alone(string? eyebrow, string? subtitle)
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Eyebrow, eyebrow)
            .Add(component => component.Title, "Discover")
            .Add(component => component.Subtitle, subtitle));

        // Assert
        cut.FindAll("hgroup").ShouldBeEmpty();
        cut.FindAll("p").ShouldBeEmpty();
        cut.Find(".site-hero__text > h1.site-hero__title").TextContent.ShouldBe("Discover");
    }

    [Fact]
    public void Eyebrow_and_subtitle_text_is_encoded()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Eyebrow, "<b>New</b> & safe")
            .Add(component => component.Title, "Discover")
            .Add(component => component.Subtitle, "<i>Small</i> groups"));

        // Assert
        cut.FindAll("b").ShouldBeEmpty();
        cut.FindAll("i").ShouldBeEmpty();
        cut.Find(".site-hero__eyebrow").TextContent.ShouldBe("<b>New</b> & safe");
        cut.Find(".site-hero__subtitle").TextContent.ShouldBe("<i>Small</i> groups");
    }

    [Fact]
    public void Image_is_rendered_eagerly_with_its_alt_and_dimensions()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Image, ResponsiveImage.Create(LinkTarget.FromString("/hero.jpg"), 1600, 900, ImageAlternative.Describe("A lake"))));

        // Assert
        var image = cut.Find("img");
        image.GetAttribute("src").ShouldBe("/hero.jpg");
        image.GetAttribute("alt").ShouldBe("A lake");
        image.GetAttribute("width").ShouldBe("1600");
        image.GetAttribute("height").ShouldBe("900");
        image.GetAttribute("loading").ShouldBe("eager");
        image.GetAttribute("fetchpriority").ShouldBe("high");
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

    [Fact]
    public void The_default_layout_is_split_and_adds_no_layout_class()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters.Add(component => component.Title, "Discover"));

        // Assert
        cut.Find(".site-hero").ClassName.ShouldBe("site-hero");
    }

    [Fact]
    public void A_cover_hero_lays_its_text_over_the_eager_image_that_follows_it()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Layout, HeroLayout.Cover)
            .Add(component => component.Image, ResponsiveImage.Create(LinkTarget.FromString("/hero.jpg"), 1600, 900, ImageAlternative.Decorative())));

        // Assert
        var root = cut.Find("section.site-hero");
        BunitElementAssertions.HasClass(root.ClassList, "site-hero--cover");
        root.Children.Select(child => child.ClassName).ShouldBe(["site-hero__text", "site-hero__media"]);
        var image = cut.Find(".site-hero__media img");
        image.GetAttribute("loading").ShouldBe("eager");
        image.GetAttribute("fetchpriority").ShouldBe("high");
    }

    [Fact]
    public void A_cover_hero_without_an_image_is_a_band_without_media()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Layout, HeroLayout.Cover));

        // Assert
        BunitElementAssertions.HasClass(cut.Find(".site-hero").ClassList, "site-hero--cover");
        cut.FindAll(".site-hero__media").ShouldBeEmpty();
    }

    [Fact]
    public void An_undefined_layout_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Discover")
            .Add(component => component.Layout, (HeroLayout)7));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Hero.Layout));
    }
}
