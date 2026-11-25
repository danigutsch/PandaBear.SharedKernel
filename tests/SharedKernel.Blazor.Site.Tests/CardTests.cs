namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the card and its grid.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class CardTests : BunitContext
{
    [Fact]
    public void Card_is_an_article_with_a_heading_link_text_image_and_actions()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Atacama")
            .Add(component => component.Href, LinkTarget.FromString("/tours/atacama"))
            .Add(component => component.Image, ResponsiveImage.Create(LinkTarget.FromString("/atacama.jpg"), 800, 600, ImageAlternative.Describe("Salt flats")))
            .Add(component => component.Actions, "<a id=\"more\" href=\"/x\">More</a>")
            .AddChildContent("<p id=\"text\">Desert</p>"));

        // Assert
        cut.Find("article").ShouldNotBeNull();
        var link = cut.Find("article h3 a");
        link.TextContent.ShouldBe("Atacama");
        link.GetAttribute("href").ShouldBe("/tours/atacama");
        var image = cut.Find("img");
        image.GetAttribute("alt").ShouldBe("Salt flats");
        image.GetAttribute("loading").ShouldBe("lazy");
        image.GetAttribute("width").ShouldBe("800");
        cut.Find(".site-card__body #text").ShouldNotBeNull();
        cut.Find(".site-card__actions #more").ShouldNotBeNull();
    }

    [Fact]
    public void Heading_level_is_configurable_and_the_title_is_plain_text_without_a_link()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Atacama")
            .Add(component => component.HeadingLevel, 4));

        // Assert
        cut.Find("h4").TextContent.ShouldBe("Atacama");
        cut.FindAll("a").ShouldBeEmpty();
        cut.FindAll("img").ShouldBeEmpty();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_article()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Atacama")
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var article = cut.Find("article");
        BunitElementAssertions.HasClass(article.ClassList, "site-card");
        BunitElementAssertions.HasClass(article.ClassList, "extra");
        article.GetAttribute("data-test").ShouldBe("x");
    }

    [Fact]
    public void Grid_wraps_its_cards_and_passes_attributes_through()
    {
        // Arrange & Act
        var cut = Render<CardGrid>(parameters => parameters
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x")
            .AddChildContent("<article id=\"one\">1</article>"));

        // Assert
        var grid = cut.Find(".site-card-grid");
        BunitElementAssertions.HasClass(grid.ClassList, "extra");
        grid.GetAttribute("data-test").ShouldBe("x");
        grid.QuerySelector("#one").ShouldNotBeNull();
    }
}
