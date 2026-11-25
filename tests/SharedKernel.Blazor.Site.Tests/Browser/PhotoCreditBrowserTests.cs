namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Verifies that localized credits remain visible and operable in Chromium.</summary>
[Testing.TestScope(Testing.TestScope.System)]
public sealed class PhotoCreditBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(320, false)]
    [InlineData(1280, false)]
    [InlineData(320, true)]
    [InlineData(1280, true)]
    public async Task Long_localized_credits_wrap_and_the_link_reaches_its_photo_by_keyboard(int width, bool useGallery)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PhotoCreditsSample>(
            chromium.Browser, width, new Dictionary<string, object?> { [nameof(PhotoCreditsSample.UseGallery)] = useGallery });
        const string terms = ".site-photo-credit__summary a[href*='creativecommons.org']";
        const string details = ".site-photo-credit__summary a[href='#serra']";
        const string source = "#serra a[href='https://archive.example/serra']";

        // Act
        var termsFocused = await InvertedSample.FocusWithKeyboard(page, terms);
        var termsOutline = await page.Style(terms, "outline-style");
        await page.Page.Keyboard.PressAsync("Tab");
        var focusedTarget = await page.Attribute(":focus-visible", "href");
        var detailsVisible = await page.IsVisible(details);
        await page.Page.Keyboard.PressAsync("Enter");
        var accessible = await page.AccessibilityTree();
        var scrollWidth = await page.ScrollWidth();
        var sourceVisible = await page.IsVisible(source);
        await page.Page.Keyboard.PressAsync("Tab");
        var focusedSource = await page.Attribute(":focus-visible", "href");

        // Assert
        termsFocused.ShouldBeTrue();
        termsOutline.ShouldBe("solid");
        focusedTarget.ShouldBe("#serra");
        detailsVisible.ShouldBeTrue();
        page.Page.Url.ShouldBe("http://site.test/#serra");
        sourceVisible.ShouldBeTrue();
        focusedSource.ShouldBe("https://archive.example/serra");
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
        accessible.ShouldContain(node => node.Role == "link" && node.Name == "Crédito completo Serra Azul");
        accessible.ShouldContain(node => node.Role == "link" && node.Name == "Fonte original Serra Azul");
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task An_inline_credit_disclosure_exposes_its_original_source_to_keyboard_users()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PhotoCreditsSample>(chromium.Browser, 320, new Dictionary<string, object?>
        {
            [nameof(PhotoCreditsSample.UseGallery)] = true,
            [nameof(PhotoCreditsSample.InlineDetails)] = true,
        });
        const string summary = "figcaption details > summary";
        const string source = "figcaption details a[href='https://archive.example/serra']";

        // Act
        var initiallyVisible = await page.IsVisible(source);
        var disclosureFocused = await InvertedSample.FocusWithKeyboard(page, summary);
        await page.Page.Keyboard.PressAsync("Enter");
        var visibleAfterOpen = await page.IsVisible(source);
        await page.Page.Keyboard.PressAsync("Tab");
        var focusedSource = await page.Attribute(":focus-visible", "href");

        // Assert
        initiallyVisible.ShouldBeFalse();
        disclosureFocused.ShouldBeTrue();
        visibleAfterOpen.ShouldBeTrue();
        focusedSource.ShouldBe("https://archive.example/serra");
    }
}
