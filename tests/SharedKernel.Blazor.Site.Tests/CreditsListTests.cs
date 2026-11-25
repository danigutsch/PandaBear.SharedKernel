using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the list of image credits.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class CreditsListTests : BunitContext
{
    private static readonly CreditItem Full = new("Ana Lima", "CC BY-SA 4.0")
    {
        Thumbnail = SiteFixtures.Image("/thumbs/atacama.jpg", ImageAlternative.Decorative()),
        Title = "Salt flats at dusk",
        AuthorHref = SiteFixtures.Link("https://commons.wikimedia.org/wiki/User:AnaLima"),
        SourceHref = SiteFixtures.Link("https://commons.wikimedia.org/wiki/File:Atacama.jpg"),
        LicenseHref = SiteFixtures.Link("https://creativecommons.org/licenses/by-sa/4.0/"),
        ChangesText = "Cropped",
    };

    /// <summary>Gets credits that are missing, have a blank author or license text, or link both the author and an untitled source.</summary>
    public static TheoryData<CreditItem?> InvalidItems { get; } = new()
    {
        null!,
        Full with { Author = " " },
        Full with { LicenseText = "" },
        Full with { Title = null },
    };

    [Fact]
    public void Credits_are_a_list_with_an_explicit_role_of_title_author_license_and_changes()
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
            ["site-credits-list__title", "site-credits-list__author", "site-credits-list__license", "site-credits-list__changes"]);
        cut.Find(".site-credits-list__title").InnerHtml.ShouldBe("<a href=\"https://commons.wikimedia.org/wiki/File:Atacama.jpg\">Salt flats at dusk</a>");
        cut.Find(".site-credits-list__author").InnerHtml.ShouldBe("<a href=\"https://commons.wikimedia.org/wiki/User:AnaLima\">Ana Lima</a>");
        cut.Find(".site-credits-list__license").InnerHtml.ShouldBe("<a href=\"https://creativecommons.org/licenses/by-sa/4.0/\" rel=\"license\">CC BY-SA 4.0</a>");
        cut.Find(".site-credits-list__changes").TextContent.ShouldBe("Cropped");
    }

    [Fact]
    public void Without_a_title_or_an_author_address_the_author_links_to_the_source()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters.Add(component => component.Items, [Full with { Title = null, AuthorHref = null }]));

        // Assert
        cut.FindAll(".site-credits-list__title").ShouldBeEmpty();
        cut.Find(".site-credits-list__author").InnerHtml.ShouldBe("<a href=\"https://commons.wikimedia.org/wiki/File:Atacama.jpg\">Ana Lima</a>");
    }

    [Fact]
    public void A_bare_credit_has_no_thumbnail_links_or_changes()
    {
        // Arrange & Act
        var cut = Render<CreditsList>(parameters => parameters.Add(component => component.Items, [new CreditItem("Ana Lima", "All rights reserved")]));

        // Assert
        cut.FindAll("a, img, .site-credits-list__thumbnail, .site-credits-list__changes, .site-credits-list__title").ShouldBeEmpty();
        cut.Find(".site-credits-list__text").Children.Select(child => child.TextContent).ShouldBe(["Ana Lima", "All rights reserved"]);
    }

    [Fact]
    public void A_shared_credit_maps_to_an_item_with_its_links_and_the_changes_text_only_when_changed()
    {
        // Arrange
        var changed = ImageCredit.Create("Ana Lima", "Salar", "https://example.org/salar", "CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/", modified: true).ShouldBeSuccess();
        var unchanged = ImageCredit.Create("Ana Lima", null, null, "Pixabay", null, modified: false).ShouldBeSuccess();

        // Act
        var changedItem = CreditItem.FromCredit(changed, "Cropped");
        var unchangedItem = CreditItem.FromCredit(unchanged, "Cropped");

        // Assert
        changedItem.ShouldBe(new CreditItem("Ana Lima", "CC BY 4.0")
        {
            Title = "Salar",
            SourceHref = SiteFixtures.Link("https://example.org/salar"),
            LicenseHref = SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/"),
            ChangesText = "Cropped",
        });
        unchangedItem.ShouldBe(new CreditItem("Ana Lima", "Pixabay"));
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
