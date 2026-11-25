using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the pager, which follows the GOV.UK Design System pagination component.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class PagerTests : BunitContext
{
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Nothing_is_rendered_when_there_is_only_one_page(int total)
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(1, 10, total));

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_is_rendered_for_a_default_window()
    {
        // Arrange & Act
        var cut = this.RenderPager(default);

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void The_pager_is_a_named_navigation_landmark_of_plain_links_and_page_one_has_the_address_without_a_page()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(2, 10, 30), configure: parameters => parameters.Add(pager => pager.Class, "gallery-pager"));

        // Assert
        var nav = cut.Find("nav");
        nav.GetAttribute("aria-label").ShouldBe("Photo pages");
        nav.GetAttribute("class").ShouldBe("site-pager gallery-pager");
        cut.FindAll("button, [onclick], form, script").ShouldBeEmpty();
        var links = cut.FindAll("li > a");
        links.Select(link => link.TextContent.Trim()).ShouldBe(["Previous", "1", "2", "3", "Next"]);
        links.Select(link => link.GetAttribute("href")).ShouldBe(["/fotos", "/fotos", "/fotos?page=2", "/fotos?page=3", "/fotos?page=3"]);
        links[0].GetAttribute("rel").ShouldBe("prev");
        links[^1].GetAttribute("rel").ShouldBe("next");
    }

    [Fact]
    public void Only_the_current_page_is_marked_as_current()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(2, 10, 30));

        // Assert
        var current = cut.FindAll("a[aria-current]");
        current.Count.ShouldBe(1);
        current[0].GetAttribute("aria-current").ShouldBe("page");
        current[0].TextContent.Trim().ShouldBe("2");
    }

    [Fact]
    public void The_first_page_has_no_previous_link_and_the_last_page_has_no_next_link()
    {
        // Arrange & Act
        var first = this.RenderPager(PageWindow.Create(1, 10, 30));
        var last = this.RenderPager(PageWindow.Create(3, 10, 30));

        // Assert
        first.FindAll("a[rel=prev]").ShouldBeEmpty();
        first.FindAll("a[rel=next]").Count.ShouldBe(1);
        last.FindAll("a[rel=next]").ShouldBeEmpty();
        last.FindAll("a[rel=prev]").Count.ShouldBe(1);
    }

    [Fact]
    public void Many_pages_show_the_first_the_last_and_the_neighbours_of_the_current_page_with_ellipses_hidden_from_assistive_technology()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(10, 10, 200));

        // Assert
        cut.FindAll("li").Select(item => item.TextContent.Trim()).ShouldBe(["Previous", "1", "…", "9", "10", "11", "…", "20", "Next"]);
        var gaps = cut.FindAll("li:not(:has(a))");
        gaps.Count.ShouldBe(2);
        gaps.ShouldAllSatisfy(gap => gap.GetAttribute("aria-hidden").ShouldBe("true"));
        gaps.ShouldAllSatisfy(gap => gap.ClassList.ShouldContain("site-pager__ellipsis"));
    }

    [Fact]
    public void A_gap_of_a_single_page_shows_that_page_instead_of_an_ellipsis()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(4, 10, 100));

        // Assert
        cut.NumberedPages().ShouldBe(["1", "2", "3", "4", "5", "…", "10"]);
    }

    [Theory]
    [InlineData(0, new[] { "1", "…", "50", "…", "100" })]
    [InlineData(2, new[] { "1", "…", "48", "49", "50", "51", "52", "…", "100" })]
    public void The_number_of_page_links_is_bounded_by_the_surrounding_pages(int surrounding, string[] expected)
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(50, 1, 100), configure: parameters => parameters.Add(pager => pager.SurroundingPages, surrounding));

        // Assert
        cut.NumberedPages().ShouldBe(expected);
    }

    [Fact]
    public void The_last_of_the_largest_number_of_pages_does_not_overflow()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(int.MaxValue, 1, int.MaxValue));

        // Assert
        cut.NumberedPages().Length.ShouldBe(4);
        cut.FindAll("a[rel=next]").ShouldBeEmpty();
    }

    [Fact]
    public void A_page_label_format_names_each_numbered_link_including_the_current_one()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(2, 10, 30), configure: parameters => parameters.Add(pager => pager.PageLabelFormat, "Page {0}"));

        // Assert
        cut.FindAll("li > a:not([rel])").Select(link => link.GetAttribute("aria-label")).ShouldBe(["Page 1", "Page 2", "Page 3"]);
        cut.Find("a[aria-current]").GetAttribute("aria-label").ShouldBe("Page 2");
        cut.Find("a[rel=prev]").HasAttribute("aria-label").ShouldBeFalse();
    }

    [Fact]
    public void Numbered_links_have_no_label_of_their_own_without_a_format()
    {
        // Arrange & Act
        var cut = this.RenderPager(PageWindow.Create(2, 10, 30));

        // Assert
        cut.FindAll("a[aria-label]").ShouldBeEmpty();
    }

    [Fact]
    public void Link_texts_are_encoded()
    {
        // Arrange & Act
        var cut = Render<Pager>(parameters => parameters
            .Add(pager => pager.Window, PageWindow.Create(2, 10, 30))
            .Add(pager => pager.PageHref, PagerRendering.Href)
            .Add(pager => pager.Label, "Pages")
            .Add(pager => pager.PreviousText, "<script>alert(1)</script>")
            .Add(pager => pager.NextText, "Next"));

        // Assert
        cut.FindAll("script").ShouldBeEmpty();
        cut.Find("a[rel=prev]").TextContent.Trim().ShouldBe("<script>alert(1)</script>");
    }

    [Fact]
    public void Missing_or_invalid_parameters_are_rejected()
    {
        // Arrange
        var window = PageWindow.Create(2, 10, 30);
        var noHref = () => Render<Pager>(parameters => parameters
            .Add(pager => pager.Window, window)
            .Add(pager => pager.Label, "Pages")
            .Add(pager => pager.PreviousText, "Previous")
            .Add(pager => pager.NextText, "Next"));
        var blankLabel = () => this.RenderPager(window, label: " ");
        var negative = () => this.RenderPager(window, configure: parameters => parameters.Add(pager => pager.SurroundingPages, -1));
        var tooMany = () => this.RenderPager(window, configure: parameters => parameters.Add(pager => pager.SurroundingPages, Pager.MaxSurroundingPages + 1));

        // Act & Assert
        noHref.ShouldThrow<ArgumentNullException>();
        blankLabel.ShouldThrow<ArgumentException>();
        negative.ShouldThrow<ArgumentOutOfRangeException>();
        tooMany.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void The_stylesheet_styles_the_pager_with_theme_tokens_only()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var start = css.IndexOf("/* Pager. */", StringComparison.Ordinal);
        var block = css[start..css.IndexOf("/*", start + 2, StringComparison.Ordinal)];

        // Assert
        block.ShouldContain(".site-pager a[aria-current=\"page\"]", StringComparison.Ordinal);
        block.ShouldContain("var(--site-color-primary)", StringComparison.Ordinal);
        block.ShouldNotContain("#", StringComparison.Ordinal);
        block.ShouldNotContain("rgb", StringComparison.Ordinal);
    }
}
