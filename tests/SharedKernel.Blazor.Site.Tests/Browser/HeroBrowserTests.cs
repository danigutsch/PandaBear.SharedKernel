namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Tests the hero and the header brand in Chromium; they skip when the browser is not installed.</summary>
public sealed class HeroBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(HeroLayout.Split, false, 375)]
    [InlineData(HeroLayout.Split, true, 1280)]
    [InlineData(HeroLayout.Cover, false, 375)]
    [InlineData(HeroLayout.Cover, false, 1280)]
    [InlineData(HeroLayout.Cover, true, 1280)]
    public async Task The_actions_sit_1_5rem_under_the_heading_whatever_the_layout(HeroLayout layout, bool subtitle, int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<HeroSample>(chromium.Browser, width, new Dictionary<string, object?>
        {
            [nameof(HeroSample.Layout)] = layout,
            [nameof(HeroSample.Subtitle)] = subtitle,
        });

        // Act
        var heading = await page.Box(subtitle ? ".site-hero__subtitle" : ".site-hero__title");
        var actions = await page.Box(".site-hero__actions");

        // Assert
        Math.Abs(actions.Y - heading.Bottom - 24).ShouldBeLessThanOrEqualTo(1);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(HeroLayout.Cover, 375)]
    [InlineData(HeroLayout.Cover, 1280)]
    [InlineData(HeroLayout.Split, 1280)]
    public async Task A_large_title_is_a_quarter_larger_than_the_level_one_heading(HeroLayout layout, int width)
    {
        // Arrange
        await using var regular = await LayoutPage.Serve<HeroSample>(chromium.Browser, width, new Dictionary<string, object?> { [nameof(HeroSample.Layout)] = layout });
        await using var large = await LayoutPage.Serve<HeroSample>(chromium.Browser, width, new Dictionary<string, object?>
        {
            [nameof(HeroSample.Layout)] = layout,
            [nameof(HeroSample.TitleScale)] = HeroTitleScale.Large,
        });

        // Act
        var regularSize = CssLength.Pixels(await regular.Style(".site-hero__title", "font-size"));
        var largeSize = CssLength.Pixels(await large.Style(".site-hero__title", "font-size"));
        var scrollWidth = await large.ScrollWidth();

        // Assert
        Math.Abs(largeSize - (regularSize * 1.25)).ShouldBeLessThanOrEqualTo(0.1);
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }
}
