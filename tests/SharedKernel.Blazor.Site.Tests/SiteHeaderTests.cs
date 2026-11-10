namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the site header and its navigation.</summary>
public sealed class SiteHeaderTests : BunitContext
{
    [Fact]
    public void Header_is_a_banner_with_a_labelled_navigation_and_the_brand()
    {
        // Arrange & Act
        var cut = this.RenderHeader();

        // Assert
        cut.Find("header").ShouldNotBeNull();
        cut.Find("nav").GetAttribute("aria-label").ShouldBe("Main");
        cut.Find(".site-header__brand a").TextContent.ShouldBe("Brand");
    }

    [Fact]
    public void Items_are_a_list_of_links_and_children_are_a_native_disclosure()
    {
        // Arrange & Act
        var cut = this.RenderHeader();

        // Assert
        cut.FindAll("nav > ul > li").Count.ShouldBe(3);
        cut.FindAll("nav > ul > li > a").Select(link => link.GetAttribute("href")).ShouldBeEquivalentTo("/", "/tours");
        var disclosure = cut.Find("nav details");
        disclosure.QuerySelector("summary")!.TextContent.ShouldBe("About");
        disclosure.QuerySelectorAll("ul a").Select(link => link.GetAttribute("href")).ShouldBeEquivalentTo("/about/team", "/about/story");
    }

    [Fact]
    public void The_current_page_is_marked_and_no_other_link_is()
    {
        // Arrange & Act
        var cut = this.RenderHeader("/tours");

        // Assert
        var current = cut.FindAll("[aria-current]");
        current.Count.ShouldBe(1);
        current[0].GetAttribute("href").ShouldBe("/tours");
        current[0].GetAttribute("aria-current").ShouldBe("page");
    }

    [Theory]
    [InlineData("/tours/")]
    [InlineData("/Tours?x=1")]
    [InlineData("/tours#top")]
    public void Current_path_matching_ignores_case_query_fragment_and_trailing_slash(string path)
    {
        // Arrange & Act
        var cut = this.RenderHeader(path);

        // Assert
        cut.Find("[aria-current]").GetAttribute("href").ShouldBe("/tours");
    }

    [Fact]
    public void A_child_page_is_marked_and_its_disclosure_is_open()
    {
        // Arrange & Act
        var cut = this.RenderHeader("/about/team");

        // Assert
        cut.Find("[aria-current]").GetAttribute("href").ShouldBe("/about/team");
        cut.Find("nav details").HasAttribute("open").ShouldBeTrue();
    }

    [Fact]
    public void Items_without_target_are_rejected()
    {
        // Arrange & Act
        var empty = () => Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Items, [new SiteNavItem("Nothing")]));

        // Assert
        empty.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_header()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var header = cut.Find("header");
        BunitElementAssertions.HasClass(header.ClassList, "site-header");
        BunitElementAssertions.HasClass(header.ClassList, "extra");
        header.GetAttribute("data-test").ShouldBe("x");
    }

    [Fact]
    public void Actions_slot_is_rendered_inside_the_header()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Actions, "<span id=\"slot\">x</span>"));

        // Assert
        cut.Find("header #slot").ShouldNotBeNull();
    }
}
