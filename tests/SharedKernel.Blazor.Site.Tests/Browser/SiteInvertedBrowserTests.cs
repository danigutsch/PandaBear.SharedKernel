namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium, with reduced motion so a hover shows at once, that buttons, the more look of a card, headings and the divider
/// keep their contrast on the primary colour of a band and a footer, and that a boxed card there keeps the look of the page; they skip
/// when the browser is not installed.
/// </summary>
public sealed class SiteInvertedBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";
    private const string Primary = "#1d4ed8";
    private const string PrimaryHover = "#1e40af";
    private const string OnPrimary = "#ffffff";
    private const string SurfaceAlt = "#f3f4f6";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_plain_card_in_a_primary_band_inverts_its_more_look_and_button_at_rest_and_on_hover()
    {
        // Arrange
        await using var page = await InvertedSample.Open(chromium.Browser);

        // Act
        var moreText = await InvertedSample.Color(page, "#plain .site-card__more", "color");
        var moreBorder = await InvertedSample.Color(page, "#plain .site-card__more", "border-top-color");
        var actionBackground = await InvertedSample.Color(page, "#plain-action", "background-color");
        var actionText = await InvertedSample.Color(page, "#plain-action", "color");
        await InvertedSample.MoveMouseTo(page, "#plain .site-card__more");
        var hoveredText = await InvertedSample.Color(page, "#plain .site-card__more", "color");
        var hoveredBorder = await InvertedSample.Color(page, "#plain .site-card__more", "border-top-color");

        // Assert
        moreText.ShouldBe(OnPrimary);
        moreBorder.ShouldBe(OnPrimary);
        SiteFixtures.Contrast(moreText, Primary).ShouldBeGreaterThanOrEqualTo(4.5);
        actionBackground.ShouldBe(OnPrimary);
        actionText.ShouldBe(Primary);
        SiteFixtures.Contrast(actionText, actionBackground).ShouldBeGreaterThanOrEqualTo(4.5);
        hoveredText.ShouldBe(OnPrimary);
        hoveredBorder.ShouldBe(OnPrimary);
        SiteFixtures.Contrast(hoveredText, Primary).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_boxed_card_in_a_primary_band_keeps_the_page_look_of_its_more_look_and_button_at_rest_and_on_hover()
    {
        // Arrange
        await using var page = await InvertedSample.Open(chromium.Browser);

        // Act
        var card = await InvertedSample.Color(page, "#boxed", "background-color");
        var moreBackground = await InvertedSample.Color(page, "#boxed .site-card__more", "background-color");
        var moreText = await InvertedSample.Color(page, "#boxed .site-card__more", "color");
        var actionText = await InvertedSample.Color(page, "#boxed-action", "color");
        var actionBorder = await InvertedSample.Color(page, "#boxed-action", "border-top-color");
        await InvertedSample.MoveMouseTo(page, "#boxed .site-card__more");
        var hoveredBackground = await InvertedSample.Color(page, "#boxed .site-card__more", "background-color");
        var hoveredText = await InvertedSample.Color(page, "#boxed .site-card__more", "color");
        await InvertedSample.MoveMouseTo(page, "#boxed-action");
        var hoveredAction = await InvertedSample.Color(page, "#boxed-action", "color");

        // Assert
        card.ShouldBe(SurfaceAlt);
        moreBackground.ShouldBe(Primary);
        moreText.ShouldBe(OnPrimary);
        SiteFixtures.Contrast(moreText, moreBackground).ShouldBeGreaterThanOrEqualTo(4.5);
        actionText.ShouldBe(Primary);
        actionBorder.ShouldBe(Primary);
        SiteFixtures.Contrast(actionText, card).ShouldBeGreaterThanOrEqualTo(4.5);
        hoveredBackground.ShouldBe(PrimaryHover);
        SiteFixtures.Contrast(hoveredText, hoveredBackground).ShouldBeGreaterThanOrEqualTo(4.5);
        hoveredAction.ShouldBe(PrimaryHover);
        SiteFixtures.Contrast(hoveredAction, card).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_heading_with_the_primary_colour_and_the_divider_take_the_on_primary_colour_in_a_primary_footer()
    {
        // Arrange
        await using var page = await InvertedSample.Open(chromium.Browser);

        // Act
        var footer = ComputedColor.ToHex(await page.Style("footer", "background-color"));
        var heading = ComputedColor.ToHex(await page.Style("#footer-heading", "color"));
        var divider = ComputedColor.ToHex(await page.Style("#footer-divider", "background-color"));

        // Assert
        footer.ShouldBe(Primary);
        heading.ShouldBe(OnPrimary);
        SiteFixtures.Contrast(heading, footer).ShouldBeGreaterThanOrEqualTo(4.5);
        divider.ShouldBe(OnPrimary);
    }
}
