using PandaBear.Blazor.Site.Theming;

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

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_text_brand_takes_the_heading_face_without_an_underline_in_the_primary_colour()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<HeroSample>(chromium.Browser, 1280);

        // Act
        var family = await page.Style("#brand", "font-family");
        var headingFamily = await page.Style(".site-hero__title", "font-family");
        var weight = await page.Style("#brand", "font-weight");
        var decoration = await page.Style("#brand", "text-decoration-line");
        var color = ComputedColor.ToHex(await page.Style("#brand", "color"));

        // Assert
        family.ShouldBe(headingFamily);
        weight.ShouldBe("700");
        decoration.ShouldBe("none");
        color.ShouldBe("#1d4ed8");
        SiteFixtures.Contrast(color, "#ffffff").ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_text_brand_shows_the_focus_ring_when_reached_with_the_keyboard()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<HeroSample>(chromium.Browser, 1280);

        // Act
        var focused = await InvertedSample.FocusWithKeyboard(page, "#brand");
        var style = await page.Style("#brand", "outline-style");
        var width = await page.Style("#brand", "outline-width");
        var ring = ComputedColor.ToHex(await page.Style("#brand", "outline-color"));

        // Assert
        focused.ShouldBeTrue();
        style.ShouldBe("solid");
        width.ShouldBe("3px");
        SiteFixtures.Contrast(ring, "#ffffff").ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_theme_that_gives_the_title_a_serif_face_gives_it_to_the_text_brand()
    {
        // Arrange
        var h1 = TypographyStyle.Create(fallback: GenericFontFamily.Serif, size: SiteTheme.Default.Typography[TypographyRole.H1].Size).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography.With(TypographyRole.H1, h1)).ShouldBeSuccess();
        await using var page = await LayoutPage.Serve<HeroSample>(chromium.Browser, 1280, theme: SiteThemeStylesheet.Render(theme));

        // Act
        var family = await page.Style("#brand", "font-family");
        var headingFamily = await page.Style(".site-hero__title", "font-family");

        // Assert
        family.ShouldBe(headingFamily);
        family.ShouldEndWith("serif", StringComparison.Ordinal);
        family.ShouldNotContain("sans-serif", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_theme_brand_colour_colours_the_text_brand_on_the_page()
    {
        // Arrange
        var brand = TypographyStyle.Create(color: ThemeColorRole.Text).ShouldBeSuccess();
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography.With(TypographyRole.Brand, brand)).ShouldBeSuccess();
        await using var page = await LayoutPage.Serve<HeroSample>(chromium.Browser, 1280, theme: SiteThemeStylesheet.Render(theme));

        // Act
        var color = ComputedColor.ToHex(await page.Style("#brand", "color"));

        // Assert
        color.ShouldBe("#1f2937");
    }
}
