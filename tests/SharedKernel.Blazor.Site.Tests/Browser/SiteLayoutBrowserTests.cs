namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests the layout of the header navigation, the card grid and the gutter container in Chromium on a phone (375px) and a desktop
/// (1280px) viewport; they skip when the browser is not installed.
/// </summary>
[Testing.TestScope(Testing.TestScope.System)]
public sealed class SiteLayoutBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task On_a_page_inside_a_group_the_group_stays_closed_and_shows_a_disclosure_marker(int width)
    {
        // Act
        await using var page = await LayoutPage.Open(chromium.Browser, width);

        // Assert
        var open = await page.GroupIsOpen();
        var sublistVisible = await page.IsVisible(".site-nav__sublist");
        var markerWidth = await page.Style(".site-nav__group > summary", "width", "::after");
        var markerBorder = await page.Style(".site-nav__group > summary", "border-right-width", "::after");
        var current = await page.Attribute(".site-nav__group > summary", "aria-current");
        open.ShouldBeFalse();
        sublistVisible.ShouldBeFalse();
        markerWidth.ShouldNotBe("0px");
        markerWidth.ShouldNotBe("auto");
        markerBorder.ShouldBe("2px");
        current.ShouldBe("true");
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task On_a_phone_an_open_group_stacks_the_navigation_without_sideways_scrolling()
    {
        // Arrange
        await using var page = await LayoutPage.Open(chromium.Browser, 375);
        var brandBefore = await page.Box(".site-header__brand");

        // Act
        await page.OpenGroup();

        // Assert
        var brand = await page.Box(".site-header__brand");
        var list = await page.Box(".site-nav__list");
        var items = await page.Boxes(".site-nav__list > .site-nav__item");
        var sublist = await page.Box(".site-nav__sublist");
        var scrollWidth = await page.ScrollWidth();
        brand.ShouldBe(brandBefore);
        list.X.ShouldBe(brand.X);
        list.Y.ShouldBeGreaterThanOrEqualTo(brand.Bottom);
        items.Select(item => item.X).ShouldAllSatisfy(left => left.ShouldBe(list.X));
        items[2].Y.ShouldBeGreaterThanOrEqualTo(sublist.Bottom);
        items[3].Y.ShouldBeGreaterThan(items[2].Y);
        scrollWidth.ShouldBeLessThanOrEqualTo(375);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task On_a_desktop_an_open_group_drops_down_over_the_page_without_moving_the_navigation()
    {
        // Arrange
        await using var page = await LayoutPage.Open(chromium.Browser, 1280);
        var headerBefore = await page.Box(".site-header");
        var itemsBefore = await page.Boxes(".site-nav__list > .site-nav__item");

        // Act
        await page.OpenGroup();

        // Assert
        var headerAfter = await page.Box(".site-header");
        var itemsAfter = await page.Boxes(".site-nav__list > .site-nav__item");
        var summary = await page.Box(".site-nav__group > summary");
        var sublist = await page.Box(".site-nav__sublist");
        headerAfter.Height.ShouldBe(headerBefore.Height);
        itemsAfter.Select(item => (item.X, item.Y)).ShouldBe(itemsBefore.Select(item => (item.X, item.Y)));
        itemsAfter.Select(item => item.Y).ShouldAllSatisfy(top => top.ShouldBe(itemsBefore[0].Y));
        sublist.Y.ShouldBeGreaterThanOrEqualTo(summary.Bottom);
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_lone_card_has_the_width_of_one_column_of_a_full_grid(int width)
    {
        // Arrange
        await using var lonePage = await LayoutPage.Open(chromium.Browser, width, cards: 1);
        await using var fullPage = await LayoutPage.Open(chromium.Browser, width, cards: 4);

        // Act
        var lone = await lonePage.Box(".site-card");
        var grid = await lonePage.Box(".site-card-grid");
        var fullCards = await fullPage.Boxes(".site-card");

        // Assert
        lone.Width.ShouldBe(fullCards[0].Width);
        if (width < 768)
        {
            lone.Width.ShouldBe(grid.Width);
        }
        else
        {
            lone.Width.ShouldBeLessThan(grid.Width / 2);
        }
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task Text_in_the_gutter_container_lines_up_with_the_section_without_its_vertical_padding(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Open(chromium.Browser, width);

        // Act
        var text = await page.Box("#gutter-text");
        var heading = await page.Box(".site-section__title");
        var container = await page.Box(".site-container");
        var section = await page.Box(".site-section");
        var paddingTop = await page.Style(".site-container", "padding-top");
        var paddingBottom = await page.Style(".site-container", "padding-bottom");

        // Assert
        text.X.ShouldBeGreaterThan(0);
        text.X.ShouldBe(heading.X);
        container.Width.ShouldBe(section.Width);
        paddingTop.ShouldBe("0px");
        paddingBottom.ShouldBe("0px");
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData("")]
    [InlineData(":root { --site-nav-font-size: 0.875rem; --site-nav-line-height: 1.7; --site-nav-letter-spacing: 0.05em; }")]
    public async Task On_a_desktop_the_language_switcher_sits_on_the_line_of_the_navigation_links_in_their_type(string theme)
    {
        // Arrange
        await using var page = await LayoutPage.Open(chromium.Browser, 1280, languages: true, theme: theme);

        // Act
        var lines = await page.Page.EvaluateAsync<double[]>(
            """
            () => [document.querySelector('.site-nav__item > a'), document.querySelector('.site-language-switcher a')].map(link => {
                const range = document.createRange();
                range.selectNodeContents(link);
                return range.getBoundingClientRect().bottom;
            })
            """);

        var navigationSize = await page.Style(".site-nav__item > a", "font-size");
        var languageSize = await page.Style(".site-language-switcher a", "font-size");
        var navigationSpacing = await page.Style(".site-nav__item > a", "letter-spacing");
        var languageSpacing = await page.Style(".site-language-switcher a", "letter-spacing");

        // Assert
        Math.Abs(lines[0] - lines[1]).ShouldBeLessThanOrEqualTo(0.5);
        languageSize.ShouldBe(navigationSize);
        languageSpacing.ShouldBe(navigationSpacing);
    }
}
