namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium, at 1280px where the content is 72rem (1152px) wide, that the smallest column width sets the number of columns
/// whatever the largest card width, and that the largest width caps each card in its column; they skip when the browser is not installed.
/// </summary>
public sealed class SiteCardGridBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(CardColumnWidth.Narrow, null, 4, 270)]
    [InlineData(CardColumnWidth.Narrow, CardColumnWidth.Narrow, 4, 256)]
    [InlineData(CardColumnWidth.Narrow, CardColumnWidth.Medium, 4, 270)]
    [InlineData(CardColumnWidth.Narrow, CardColumnWidth.Wide, 4, 270)]
    [InlineData(CardColumnWidth.Medium, null, 3, 368)]
    [InlineData(CardColumnWidth.Medium, CardColumnWidth.Medium, 3, 352)]
    [InlineData(CardColumnWidth.Medium, CardColumnWidth.Wide, 3, 368)]
    [InlineData(CardColumnWidth.Wide, null, 2, 564)]
    [InlineData(CardColumnWidth.Wide, CardColumnWidth.Wide, 2, 512)]
    public async Task The_smallest_width_sets_the_columns_and_the_largest_caps_each_card(CardColumnWidth min, CardColumnWidth? max, int columns, double cardWidth)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GridSample>(chromium.Browser, 1280, GridSample.Parameters(min, max));

        // Act
        var grid = await page.Box(".site-card-grid");
        var cards = await page.Boxes("article.site-card");

        // Assert
        grid.Width.ShouldBe(1152);
        cards.Select(card => card.Y).Distinct().Count().ShouldBe((8 + columns - 1) / columns);
        cards.Take(columns).Select(card => card.Y).Distinct().Count().ShouldBe(1);
        cards.ShouldAllSatisfy(card => Math.Abs(card.Width - cardWidth).ShouldBeLessThanOrEqualTo(0.5));
        cards[0].X.ShouldBe(grid.X);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Capped_cards_lie_in_the_middle_of_their_columns_when_the_grid_centres_them()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GridSample>(chromium.Browser, 1280, GridSample.Parameters(CardColumnWidth.Narrow, CardColumnWidth.Narrow, SiteAlignment.Center));

        // Act
        var grid = await page.Box(".site-card-grid");
        var cards = await page.Boxes("article.site-card");

        // Assert
        cards[0].Width.ShouldBe(256);
        var column = (1152 - (3 * 24)) / 4.0;
        var middle = cards[0].X + (cards[0].Width / 2);
        Math.Abs(middle - (grid.X + (column / 2))).ShouldBeLessThanOrEqualTo(0.5);
    }
}
