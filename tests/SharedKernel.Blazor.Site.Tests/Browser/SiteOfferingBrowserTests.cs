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
}
