namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium, on a phone (375px) and a desktop (1280px) viewport, that a card does not inherit the centred text of the band
/// around it and is centred only by its own alignment; they skip when the browser is not installed.
/// </summary>
[Testing.TestScope(Testing.TestScope.System)]
public sealed class SiteCardAlignmentBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const double Padding = 16;

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_card_in_a_centred_band_stays_at_the_start_unless_it_is_centred(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<AlignedCardsSample>(chromium.Browser, width);

        // Act
        var band = await page.Style(".site-band", "text-align");
        var startAlign = await page.Style("#start", "text-align");
        var centerAlign = await page.Style("#center", "text-align");
        var startCard = await page.Box("#start");
        var startMore = await page.Box("#start .site-card__more");
        var startByline = await page.Box("#start .site-byline__photo, #start .site-byline__text");
        var centerCard = await page.Box("#center");
        var centerMore = await page.Box("#center .site-card__more");

        // Assert
        band.ShouldBe("center");
        startAlign.ShouldBe("start");
        centerAlign.ShouldBe("center");
        Math.Abs(startMore.X - (startCard.X + Padding + 1)).ShouldBeLessThanOrEqualTo(1);
        Math.Abs(startByline.X - (startCard.X + Padding + 1)).ShouldBeLessThanOrEqualTo(1);
        var centerMiddle = centerCard.X + (centerCard.Width / 2);
        Math.Abs(centerMore.X + (centerMore.Width / 2) - centerMiddle).ShouldBeLessThanOrEqualTo(1);
    }
}
