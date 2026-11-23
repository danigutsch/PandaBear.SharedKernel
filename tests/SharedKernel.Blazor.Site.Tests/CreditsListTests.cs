namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the list of image credits.</summary>
public sealed class CreditsListTests : BunitContext
{
    private static readonly CreditItem Full = new(
        SiteFixtures.Image("/thumbs/atacama.jpg", ImageAlternative.Decorative()),
        "Salt flats at dusk",
        "Ana Lima",
        SiteFixtures.Link("https://commons.wikimedia.org/wiki/File:Atacama.jpg"),
        "CC BY-SA 4.0",
        SiteFixtures.Link("https://creativecommons.org/licenses/by-sa/4.0/"),
        "Cropped");

    /// <summary>Gets credits that are missing or have a blank author or licence text.</summary>
    public static TheoryData<CreditItem?> InvalidItems { get; } = new()
    {
        null!,
        Full with { Author = " " },
        Full with { LicenceText = "" },
    };

    [Fact]
    public void Credits_are_a_list_with_an_explicit_role_of_title_author_licence_and_changes()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters.Add(component => component.Items, [Full]));

        // Assert
        var list = cut.Find("ul");
        list.ClassName.ShouldBe("site-credits-list");
        list.GetAttribute("role").ShouldBe("list");
        var item = cut.Find("li.site-credits-list__item");
        item.Children.Select(child => child.ClassName).ShouldBe(["site-credits-list__thumbnail", "site-credits-list__text"]);
        cut.Find(".site-credits-list__thumbnail img").GetAttribute("loading").ShouldBe("lazy");
        cut.Find(".site-credits-list__text").Children.Select(child => child.ClassName).ShouldBe(
            ["site-credits-list__title", "site-credits-list__author", "site-credits-list__licence", "site-credits-list__changes"]);
        cut.Find(".site-credits-list__title").InnerHtml.ShouldBe("<a href=\"https://commons.wikimedia.org/wiki/File:Atacama.jpg\">Salt flats at dusk</a>");
        cut.Find(".site-credits-list__author").InnerHtml.ShouldBe("Ana Lima");
        cut.Find(".site-credits-list__licence").InnerHtml.ShouldBe("<a href=\"https://creativecommons.org/licenses/by-sa/4.0/\" rel=\"license\">CC BY-SA 4.0</a>");
        cut.Find(".site-credits-list__changes").TextContent.ShouldBe("Cropped");
    }

    [Fact]
    public void Without_a_title_the_author_links_to_the_source()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters.Add(component => component.Items, [Full with { Title = null }]));

        // Assert
        cut.FindAll(".site-credits-list__title").ShouldBeEmpty();
        cut.Find(".site-credits-list__author").InnerHtml.ShouldBe("<a href=\"https://commons.wikimedia.org/wiki/File:Atacama.jpg\">Ana Lima</a>");
    }

    [Fact]
    public void A_bare_credit_has_no_thumbnail_links_or_changes()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters.Add(component => component.Items, [new CreditItem(null, null, "Ana Lima", null, "All rights reserved", null, null)]));

        // Assert
        cut.FindAll("a, img, .site-credits-list__thumbnail, .site-credits-list__changes, .site-credits-list__title").ShouldBeEmpty();
        cut.Find(".site-credits-list__text").Children.Select(child => child.TextContent).ShouldBe(["Ana Lima", "All rights reserved"]);
    }

    [Fact]
    public void An_empty_list_renders_nothing()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters.Add(component => component.Items, []));

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Class_and_attributes_reach_the_list()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters
            .Add(component => component.Items, [Full])
            .Add(component => component.Class, "photo-credits")
            .AddUnmatched("aria-labelledby", "credits"));

        // Assert
        var list = cut.Find("ul");
        list.ClassName.ShouldBe("site-credits-list photo-credits");
        list.GetAttribute("aria-labelledby").ShouldBe("credits");
    }

    [Theory]
    [MemberData(nameof(InvalidItems))]
    public void An_invalid_credit_is_refused(CreditItem? item)
    {
        // Arrange & Act
        var render = () => Render<CreditsList>(parameters => parameters.Add(component => component.Items, [item!]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(CreditsList.Items));
    }
}
