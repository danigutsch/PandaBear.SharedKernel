using static PandaBear.Blazor.Site.Tests.SiteFixtures;

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
    [InlineData("tours")]
    [InlineData("tours/")]
    [InlineData("Tours?x=1#top")]
    public void Current_path_matching_ignores_case_query_fragment_leading_and_trailing_slash(string path)
    {
        // Arrange & Act
        var cut = this.RenderHeader(path);

        // Assert
        cut.Find("[aria-current]").GetAttribute("href").ShouldBe("/tours");
    }

    [Fact]
    public void A_child_page_is_marked_and_its_group_is_marked_but_stays_closed()
    {
        // Arrange & Act
        var cut = this.RenderHeader("/about/team");

        // Assert
        cut.Find("[aria-current=page]").GetAttribute("href").ShouldBe("/about/team");
        cut.Find("nav details").HasAttribute("open").ShouldBeFalse();
        cut.Find("nav details > summary").GetAttribute("aria-current").ShouldBe("true");
        cut.FindAll("[aria-current]").Count.ShouldBe(2);
    }

    [Fact]
    public void A_group_without_the_current_page_is_neither_marked_nor_open()
    {
        // Arrange & Act
        var cut = this.RenderHeader("/tours");

        // Assert
        cut.Find("nav details").HasAttribute("open").ShouldBeFalse();
        cut.Find("nav details > summary").HasAttribute("aria-current").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void The_base_relative_path_of_the_home_page_marks_the_home_item(string path)
    {
        // Arrange & Act
        var cut = this.RenderHeader(path);

        // Assert
        cut.Find("[aria-current]").GetAttribute("href").ShouldBe("/");
    }

    [Fact]
    public void A_path_that_only_starts_like_an_item_is_not_current()
    {
        // Arrange & Act
        var cut = this.RenderHeader("tours-extra");

        // Assert
        cut.FindAll("[aria-current]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_navigation_without_a_label_is_rejected(string label)
    {
        // Arrange & Act
        var render = () => Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, label)
            .Add(component => component.Items, NavItems));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(SiteHeader.Label));
    }

    [Fact]
    public void A_header_without_items_needs_no_label()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters.Add(component => component.Brand, "<a href=\"/\">Brand</a>"));

        // Assert
        cut.FindAll("nav").ShouldBeEmpty();
    }

    [Fact]
    public void Items_without_target_are_rejected()
    {
        // Arrange & Act
        var render = () => this.RenderHeaderItems(new SiteNavItem("Nothing"));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(SiteHeader.Items));
    }

    [Fact]
    public void A_null_item_or_child_is_rejected()
    {
        // Arrange & Act
        var item = () => this.RenderHeaderItems(null!);
        var child = () => this.RenderHeaderItems(new SiteNavItem("Group", Children: [null!]));

        // Assert
        item.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(SiteHeader.Items));
        child.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(SiteHeader.Items));
    }

    [Fact]
    public void An_item_with_both_a_target_and_children_is_rejected()
    {
        // Arrange & Act
        var render = () => this.RenderHeaderItems(new SiteNavItem("Group", Link("/group"), [new SiteNavItem("Child", Link("/child"))]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(SiteHeader.Items));
    }

    [Fact]
    public void Children_deeper_than_one_level_are_rejected()
    {
        // Arrange
        var grandchild = new SiteNavItem("Grandchild", Link("/a/b/c"));

        // Act
        var render = () => this.RenderHeaderItems(new SiteNavItem("Group", Children: [new SiteNavItem("Child", Children: [grandchild])]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(SiteHeader.Items));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void An_item_without_text_is_rejected(string text)
    {
        // Arrange & Act
        var render = () => this.RenderHeaderItems(new SiteNavItem(text, Link("/x")));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(SiteHeader.Items));
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
