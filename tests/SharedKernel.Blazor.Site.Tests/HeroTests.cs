namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the hero.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class HeroTests : BunitContext
{
    [Fact]
    public void Hero_has_one_level_one_heading_subtitle_and_actions()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Discover")
            .Add(component => component.Subtitle, "Small groups")
            .Add(component => component.Actions, "<a id=\"cta\" href=\"/tours\">Tours</a>"));

        // Assert
        cut.FindAll("h1").Count.ShouldBe(1);
        cut.Find("h1").TextContent.ShouldBe("Discover");
        cut.Find(".site-hero__subtitle").TextContent.ShouldBe("Small groups");
        cut.Find(".site-hero__actions #cta").ShouldNotBeNull();
        cut.FindAll("img").ShouldBeEmpty();
        cut.FindAll(".site-image-credit").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HeroLayout.Split)]
    [InlineData(HeroLayout.Cover)]
    public void A_hero_image_credit_links_to_its_attribution_and_license(HeroLayout layout)
    {
        // Arrange
        var credit = new ImageCreditLine("Photo: Ana Lima", "CC BY 4.0",
            SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/"), SiteFixtures.Link("/credits#hero"));

        // Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Discover")
            .Add(component => component.Layout, layout)
            .Add(component => component.Image, SiteFixtures.PictureImage())
            .Add(component => component.ImageCredit, credit));

        // Assert
        cut.Find(".site-hero__media img").GetAttribute("fetchpriority").ShouldBe("high");
        var caption = cut.Find("small.site-image-credit");
        caption.QuerySelector("a[href='/credits#hero']").ShouldNotBeNull().TextContent.ShouldBe("Photo: Ana Lima");
        caption.QuerySelector("a[rel='license']").ShouldNotBeNull().GetAttribute("href")
            .ShouldBe("https://creativecommons.org/licenses/by/4.0/");
        cut.FindAll(".site-hero--cover .site-hero__media .site-image-credit").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Small groups")]
    public void The_actions_directly_follow_the_heading_so_their_margin_meets_the_heading_margin(string? subtitle)
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Discover")
            .Add(component => component.Subtitle, subtitle)
            .Add(component => component.Actions, "<a href=\"/tours\">Tours</a>"));

        // Assert
        var text = cut.Find(".site-hero__text");
        text.Children.Select(child => child.TagName).ShouldBe([subtitle is null ? "H1" : "HGROUP", "DIV"]);
        text.LastElementChild!.ClassName.ShouldBe("site-hero__actions");
    }

    [Fact]
    public void Eyebrow_comes_before_the_heading_and_subtitle_after_it_in_one_hgroup()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Eyebrow, "Since 2004")
            .Add(component => component.Heading, "Discover")
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
            .Add(component => component.Heading, "Discover"));

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
            .Add(component => component.Heading, "Discover")
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
            .Add(component => component.Heading, "Discover")
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
            .Add(component => component.Heading, "Discover")
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
            .Add(component => component.Heading, "Discover")
            .Add(component => component.Image, ResponsiveImage.Create(LinkTarget.FromString("/hero.jpg"), 1600, 900, ImageAlternative.Describe("A lake"))));

        // Assert
        var image = cut.Find("img");
        image.GetAttribute("src").ShouldBe("/hero.jpg");
        image.GetAttribute("alt").ShouldBe("A lake");
        image.GetAttribute("width").ShouldBe("1600");
        image.GetAttribute("height").ShouldBe("900");
        image.GetAttribute("loading").ShouldBe("eager");
        image.GetAttribute("fetchpriority").ShouldBe("high");
        cut.FindAll(".site-image-credit").ShouldBeEmpty();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_root()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Discover")
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
        var cut = Render<Hero>(parameters => parameters.Add(component => component.Heading, "Discover"));

        // Assert
        cut.Find(".site-hero").ClassName.ShouldBe("site-hero");
    }

    [Fact]
    public void A_cover_hero_lays_its_text_over_the_eager_image_that_follows_it()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Discover")
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
            .Add(component => component.Heading, "Discover")
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
            .Add(component => component.Heading, "Discover")
            .Add(component => component.Layout, (HeroLayout)7));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Hero.Layout));
    }

    [Fact]
    public void A_cover_hero_is_a_page_cover_by_default_and_a_landing_cover_on_request()
    {
        // Arrange & Act
        var page = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Layout, HeroLayout.Cover));
        var landing = Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Travel with feeling")
            .Add(component => component.Layout, HeroLayout.Cover)
            .Add(component => component.CoverHeight, CoverHeight.Landing));

        // Assert
        page.Find(".site-hero").ClassName.ShouldBe("site-hero site-hero--cover");
        landing.Find(".site-hero").ClassName.ShouldBe("site-hero site-hero--cover site-hero--landing");
    }

    [Fact]
    public void A_landing_height_on_a_split_hero_is_rejected_instead_of_ignored()
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.CoverHeight, CoverHeight.Landing));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Hero.CoverHeight));
    }

    [Fact]
    public void An_undefined_cover_height_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .Add(component => component.Layout, HeroLayout.Cover)
            .Add(component => component.CoverHeight, (CoverHeight)4));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Hero.CoverHeight));
    }
}
