namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests the primary footer, its separated lists and the tap targets in Chromium, on a phone (390px) and a desktop (1280px) viewport;
/// they skip when the browser is not installed. The page's lists are the footer's six separated items and the switcher's two languages.
/// </summary>
public sealed class SiteFooterBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";
    private const double TapTarget = 44;

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_phone_footer_and_language_links_are_at_least_44px_tap_targets()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<FooterSample>(chromium.Browser, 390);

        // Act
        var footerLinks = await page.Boxes(".site-footer a");
        var languageLinks = await page.Boxes(".site-language-switcher a");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        footerLinks.Count.ShouldBe(5);
        footerLinks.ShouldAllSatisfy(link => link.Height.ShouldBeGreaterThanOrEqualTo(TapTarget));
        languageLinks.ShouldAllSatisfy(link => link.Height.ShouldBeGreaterThanOrEqualTo(TapTarget));
        languageLinks.ShouldAllSatisfy(link => link.Width.ShouldBeGreaterThanOrEqualTo(TapTarget));
        scrollWidth.ShouldBeLessThanOrEqualTo(390);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(390)]
    [InlineData(1280)]
    public async Task A_label_and_its_number_stay_on_one_line(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<FooterSample>(chromium.Browser, width);

        // Act
        var label = await page.Box("#phone-label");
        var phone = await page.Box("#phone");
        var item = await page.Box("#contact li:nth-child(2)");

        // Assert
        var labelMiddle = label.Y + (label.Height / 2);
        var phoneMiddle = phone.Y + (phone.Height / 2);
        Math.Abs(labelMiddle - phoneMiddle).ShouldBeLessThanOrEqualTo(1);
        phone.X.ShouldBeGreaterThan(label.X);
        (await page.Box("#contact")).Width.ShouldBeGreaterThanOrEqualTo(item.Width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_bars_are_drawn_but_not_exposed_to_assistive_technology()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<FooterSample>(chromium.Browser, 1280);

        // Act
        var bar = await page.Style("#legal li:nth-child(2)", "content", "::before");
        var first = await page.Style("#legal li:nth-child(1)", "content", "::before");
        var nodes = await page.AccessibilityTree();

        // Assert
        bar.ShouldContain("|", StringComparison.Ordinal);
        first.ShouldBe("none");
        nodes.ShouldAllSatisfy(node => node.Name.ShouldNotContain("|", StringComparison.Ordinal));
        nodes.Count(node => node.Role == "listitem").ShouldBe(6 + 2);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_primary_footer_is_a_full_width_centred_band_with_on_primary_text()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<FooterSample>(chromium.Browser, 1280);

        // Act
        var footer = await page.Box("footer");
        var background = await page.Style("footer", "background-color");
        var link = await page.Style("#phone", "color");
        var align = await page.Style("footer", "text-align");

        // Assert
        footer.X.ShouldBe(0);
        footer.Width.ShouldBe(1280);
        background.ShouldBe("rgb(29, 78, 216)");
        link.ShouldBe("rgb(255, 255, 255)");
        align.ShouldBe("center");
    }
}
