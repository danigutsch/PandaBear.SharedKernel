using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the site header and its navigation.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
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

    [Fact]
    public void A_header_above_the_page_has_no_placement_class()
    {
        // Arrange & Act
        var cut = this.RenderHeader();

        // Assert
        cut.Find("header").ClassName.ShouldBe("site-header");
    }

    [Fact]
    public void A_header_over_the_hero_is_marked_for_the_overlay()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Items, NavItems)
            .Add(component => component.Placement, HeaderPlacement.OverHero));

        // Assert
        BunitElementAssertions.HasClass(cut.Find("header").ClassList, "site-header--over-hero");
    }

    [Fact]
    public void An_undefined_placement_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<SiteHeader>(parameters => parameters.Add(component => component.Placement, (HeaderPlacement)5));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(SiteHeader.Placement));
    }

    [Fact]
    public void Without_a_menu_label_the_navigation_and_actions_are_not_collapsible()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Items, NavItems)
            .Add(component => component.Actions, "<span id=\"slot\">x</span>"));

        // Assert
        cut.FindAll(".site-header__menu").ShouldBeEmpty();
        cut.Find(".site-header__inner > nav.site-nav").ShouldNotBeNull();
        cut.Find(".site-header__inner > .site-header__actions #slot").ShouldNotBeNull();
    }

    [Fact]
    public void A_menu_label_puts_the_one_navigation_and_the_actions_behind_a_closed_disclosure()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Brand, "<a href=\"/\">Brand</a>")
            .Add(component => component.Items, NavItems)
            .Add(component => component.MenuLabel, "Menu")
            .Add(component => component.Actions, "<span id=\"slot\">x</span>"));

        // Assert
        var menu = cut.Find(".site-header__inner > details.site-header__menu");
        menu.HasAttribute("open").ShouldBeFalse();
        menu.Children.Select(child => child.TagName).ShouldBe(["SUMMARY", "NAV", "DIV"]);
        var toggle = menu.Children[0];
        toggle.ClassName.ShouldBe("site-header__toggle");
        toggle.TextContent.Trim().ShouldBe("Menu");
        menu.Children[1].GetAttribute("aria-label").ShouldBe("Main");
        menu.Children[2].ClassName.ShouldBe("site-header__actions");
        menu.QuerySelector("#slot").ShouldNotBeNull();
        cut.FindAll("nav").Count.ShouldBe(1);
        cut.Find(".site-header__inner").Children[0].ClassName.ShouldBe("site-header__brand");
    }

    [Fact]
    public void Inside_the_menu_groups_stay_disclosures_and_the_current_page_is_marked()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Items, NavItems)
            .Add(component => component.MenuLabel, "Menu")
            .Add(component => component.CurrentPath, "/about/team"));

        // Assert
        cut.Find(".site-header__menu nav details.site-nav__group > summary").GetAttribute("aria-current").ShouldBe("true");
        cut.Find(".site-header__menu [aria-current=page]").GetAttribute("href").ShouldBe("/about/team");
        cut.Find(".site-header__menu").HasAttribute("open").ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_menu_label_leaves_the_menu_out(string? label)
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Items, NavItems)
            .Add(component => component.MenuLabel, label));

        // Assert
        cut.FindAll("details.site-header__menu").ShouldBeEmpty();
    }

    [Fact]
    public void A_header_with_nothing_to_collapse_has_no_menu()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Brand, "<a href=\"/\">Brand</a>")
            .Add(component => component.MenuLabel, "Menu"));

        // Assert
        cut.FindAll(".site-header__menu").ShouldBeEmpty();
    }

    [Fact]
    public void The_menu_label_is_encoded()
    {
        // Arrange & Act
        var cut = Render<SiteHeader>(parameters => parameters
            .Add(component => component.Label, "Main")
            .Add(component => component.Items, NavItems)
            .Add(component => component.MenuLabel, "<b>Menu</b>"));

        // Assert
        cut.FindAll(".site-header__toggle b").ShouldBeEmpty();
        cut.Find(".site-header__toggle").TextContent.Trim().ShouldBe("<b>Menu</b>");
    }
}
