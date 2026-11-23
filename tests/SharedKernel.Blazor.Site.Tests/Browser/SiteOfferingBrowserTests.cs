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

}
