using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests the theme preview in Chromium on an Interactive Server page whose policy is <c>style-src 'self' 'nonce-…'</c>, without
/// <c>'unsafe-inline'</c>; they skip when the browser is not installed.
/// </summary>
public sealed class ThemePreviewStrictPolicyTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_sample_headings_keep_the_heading_typography_without_a_heading_role()
    {
        // Arrange
        await using var host = await PreviewHost.Start(new PreviewHostOptions(RenderNonce: true));

        // Act
        await using var page = await PreviewPage.Open(chromium.Browser, host);

        // Assert
        var headings = await page.HeadingCount();
        var typography = await page.SampleTypography();
        headings.ShouldBe(0);
        typography.Select(style => style.Weight).ShouldBe(["700", "700", "700", "700", "400"]);
        typography[0].Size.ShouldBeGreaterThan(typography[1].Size);
        typography[1].Size.ShouldBeGreaterThan(typography[2].Size);
        typography[2].Size.ShouldBeGreaterThan(typography[3].Size);
        typography[3].Size.ShouldBeGreaterThan(typography[4].Size);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_preview_with_the_response_nonce_is_applied_without_a_violation()
    {
        // Arrange
        await using var host = await PreviewHost.Start(new PreviewHostOptions(RenderNonce: true));

        // Act
        await using var page = await PreviewPage.Open(chromium.Browser, host);

        // Assert
        var headingColor = await page.HeadingColor();
        var styleNonce = await page.StyleNonce();
        var violations = await page.Violations();
        headingColor.ShouldBe(PreviewPage.Rgb(SiteTheme.Default.Colors.Text.Light.Value));
        styleNonce.ShouldBe(page.Nonce);
        violations.ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_interactive_change_edits_the_allowed_style_element_and_still_applies()
    {
        // Arrange
        await using var host = await PreviewHost.Start(new PreviewHostOptions(RenderNonce: true));
        await using var page = await PreviewPage.Open(chromium.Browser, host);
        await page.MarkStyle();

        // Act
        await page.ChangeText();

        // Assert
        var sameElement = await page.StyleIsMarked();
        var headingColor = await page.HeadingColor();
        var violations = await page.Violations();
        sameElement.ShouldBeTrue();
        headingColor.ShouldBe(PreviewPage.Rgb(PreviewSample.ChangedText));
        violations.ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_replaced_style_element_carries_the_nonce_again_and_still_applies()
    {
        // Arrange
        await using var host = await PreviewHost.Start(new PreviewHostOptions(RenderNonce: true));
        await using var page = await PreviewPage.Open(chromium.Browser, host);
        await page.ChangeText();
        await page.MarkStyle();

        // Act
        await page.ReplacePreview();

        // Assert
        var sameElement = await page.StyleIsMarked();
        var styleNonce = await page.StyleNonce();
        var headingColor = await page.HeadingColor();
        var violations = await page.Violations();
        sameElement.ShouldBeFalse();
        styleNonce.ShouldBe(page.Nonce);
        headingColor.ShouldBe(PreviewPage.Rgb(PreviewSample.ChangedText));
        violations.ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_style_element_without_the_response_nonce_is_blocked_and_reported()
    {
        // Arrange
        await using var host = await PreviewHost.Start(new PreviewHostOptions(RenderNonce: false));

        // Act
        await using var page = await PreviewPage.Open(chromium.Browser, host);

        // Assert
        var headingColor = await page.HeadingColor();
        var violations = await page.Violations();
        headingColor.ShouldNotBe(PreviewPage.Rgb(SiteTheme.Default.Colors.Text.Light.Value));
        violations.ShouldContain(violation => violation.StartsWith("style-src", StringComparison.Ordinal));
    }
}
