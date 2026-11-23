namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Tests the offering display atoms in Chromium at 1280px; they skip when the browser is not installed.</summary>
public sealed class SiteOfferingBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    /// <summary>Gets every place a badge is shown with every tone.</summary>
    public static TheoryData<string, StatusTone> ContextsAndTones => OfferingSample.BadgeCases;

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [MemberData(nameof(ContextsAndTones))]
    public async Task A_status_badge_keeps_its_own_colours_at_4_5_to_1_on_the_page_and_on_the_primary_colour(string context, StatusTone tone)
    {
        // Arrange
        var colors = SiteFiles.Colors(dark: false);
        var name = OfferingSample.Name(tone);
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);
        var selector = $"#{context}-{name}";

        // Act
        var background = ComputedColor.ToHex(await page.Style(selector, "background-color"));
        var text = ComputedColor.ToHex(await page.Style(selector, "color"));

        // Assert
        background.ShouldBe(colors[$"color-status-{name}"]);
        text.ShouldBe(colors[$"color-on-status-{name}"]);
        SiteFixtures.Contrast(text, background).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_inline_fact_grid_is_one_row_without_borders_in_a_card_of_three_to_a_row()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var items = await page.Boxes("#tour .site-fact-grid__item");
        var card = await page.Box("#tour");
        var border = await page.Style("#tour .site-fact-grid--inline", "border-top-width");
        var itemBorder = await page.Style("#tour .site-fact-grid__item", "border-bottom-width");

        // Assert
        items.Count.ShouldBe(3);
        items.Select(item => item.Y).Distinct().Count().ShouldBe(1);
        items.ShouldAllSatisfy(item => item.Right.ShouldBeLessThanOrEqualTo(card.Right));
        border.ShouldBe("0px");
        itemBorder.ShouldBe("0px");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_inline_fact_grid_wraps_inside_its_card_on_a_narrow_phone_instead_of_scrolling()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 320);

        // Act
        var items = await page.Boxes("#tour .site-fact-grid__item");
        var card = await page.Box("#tour");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        items.ShouldAllSatisfy(item => item.Right.ShouldBeLessThanOrEqualTo(card.Right));
        scrollWidth.ShouldBeLessThanOrEqualTo(320);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task A_price_amount_stays_on_one_line_inside_its_list_beside_a_long_label(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, width);

        // Act
        var amount = await page.Box("#price-list .site-price-list__amount data");
        var line = await page.Box("#price-list .site-price-list__note");
        var list = await page.Box("#price-list");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        amount.Height.ShouldBeLessThan(line.Height * 2);
        amount.Right.ShouldBeLessThanOrEqualTo(list.Right);
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_price_list_with_a_heading_ID_is_a_group_named_by_its_heading()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var tree = await page.AccessibilityTree();

        // Assert
        tree.ShouldContain(new AccessibleNode("group", "Prices", null));
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData("#price-list", "#ffffff")]
    [InlineData("#band-prices", "#1d4ed8")]
    public async Task A_price_note_reaches_4_5_to_1_on_the_page_and_on_the_primary_colour(string list, string background)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var note = ComputedColor.ToHex(await page.Style(list + " .site-price-list__note", "color"));
        var amount = ComputedColor.ToHex(await page.Style(list + " .site-price-list__amount", "color"));

        // Assert
        SiteFixtures.Contrast(note, background).ShouldBeGreaterThanOrEqualTo(4.5);
        SiteFixtures.Contrast(amount, background).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_from_price_keeps_its_amount_on_one_line_in_a_card()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 320);

        // Act
        var amount = await page.Box("#tour .site-from-price__amount");
        var weight = await page.Style("#tour .site-from-price__amount", "font-weight");
        var whiteSpace = await page.Style("#tour .site-from-price__amount", "white-space");

        // Assert
        amount.Height.ShouldBeLessThan(40);
        weight.ShouldBe("700");
        whiteSpace.ShouldBe("nowrap");
    }
}
