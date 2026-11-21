namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium, on a phone (375px) and a desktop (1280px) viewport, a split with its media on the start side, the grid of fact
/// cards and the row of actions; they skip when the browser is not installed.
/// </summary>
public sealed class SitePartsBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";
    private const string Primary = "rgb(29, 78, 216)";
    private const double Border = 1;

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_start_split_shows_the_media_before_the_text_on_a_desktop_and_after_it_on_a_phone(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width);
        await page.ShowLoaded("#day");

        // Act
        var main = await page.Box("#day .site-split__main");
        var media = await page.Box("#day .site-split__media");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        if (width < 768)
        {
            media.Y.ShouldBeGreaterThanOrEqualTo(main.Bottom);
            media.X.ShouldBe(main.X);
        }
        else
        {
            main.X.ShouldBeGreaterThanOrEqualTo(media.X + media.Width);
            media.Y.ShouldBeLessThan(main.Bottom);
            Math.Abs(media.Width - main.Width).ShouldBeLessThanOrEqualTo(1);
        }

        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task Fact_cards_share_the_full_width_of_the_grid_in_equal_columns(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width);

        // Act
        var grid = await page.Box(".site-fact-grid");
        var cards = await page.Boxes(".site-fact-grid__item");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        cards.Count.ShouldBe(5);
        cards[0].X.ShouldBe(grid.X + Border);
        if (width < 768)
        {
            cards.ShouldAllSatisfy(card => card.X.ShouldBe(cards[0].X));
            cards.ShouldAllSatisfy(card => Math.Abs(card.X + card.Width - (grid.X + grid.Width)).ShouldBeLessThanOrEqualTo(0.1));
        }
        else
        {
            cards.Select(card => card.Y).Distinct().Count().ShouldBe(1);
            cards.ShouldAllSatisfy(card => Math.Abs(card.Width - cards[0].Width).ShouldBeLessThanOrEqualTo(1));
            Math.Abs(cards[4].X + cards[4].Width - (grid.X + grid.Width)).ShouldBeLessThanOrEqualTo(0.1);
        }

        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_fact_card_centres_its_primary_icon_and_bold_primary_label_in_a_bordered_cell()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, 1280);

        // Act
        var card = await page.Box(".site-fact-grid__item");
        var icon = await page.Box(".site-fact-grid__icon svg");
        var labelColor = await page.Style(".site-fact-grid__label", "color");
        var labelWeight = await page.Style(".site-fact-grid__label", "font-weight");
        var iconColor = await page.Style(".site-fact-grid__icon", "color");
        var iconFill = await page.Style(".site-fact-grid__icon svg", "fill");
        var align = await page.Style(".site-fact-grid__item", "text-align");
        var valueMargin = await page.Style(".site-fact-grid__value", "margin-inline-start");
        var border = await page.Style(".site-fact-grid__item", "border-inline-end-width");

        // Assert
        Math.Abs((icon.X + (icon.Width / 2)) - (card.X + (card.Width / 2))).ShouldBeLessThanOrEqualTo(1);
        icon.Width.ShouldBeGreaterThan(0);
        labelColor.ShouldBe(Primary);
        labelWeight.ShouldBe("700");
        iconColor.ShouldBe(Primary);
        iconFill.ShouldBe(Primary);
        align.ShouldBe("center");
        valueMargin.ShouldBe("0px");
        border.ShouldBe("1px");
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task An_action_row_spreads_its_actions_on_a_desktop_and_stacks_them_centred_on_a_phone(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width);

        // Act
        var row = await page.Box(".site-action-row");
        var actions = await page.Boxes(".site-action-row > *");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        actions.Count.ShouldBe(3);
        if (width < 768)
        {
            actions[1].Y.ShouldBeGreaterThanOrEqualTo(actions[0].Bottom);
            actions[2].Y.ShouldBeGreaterThanOrEqualTo(actions[1].Bottom);
            actions.ShouldAllSatisfy(action => Math.Abs((action.X + (action.Width / 2)) - (row.X + (row.Width / 2))).ShouldBeLessThanOrEqualTo(1));
        }
        else
        {
            actions.ShouldAllSatisfy(action => Math.Abs((action.Y + (action.Height / 2)) - (actions[0].Y + (actions[0].Height / 2))).ShouldBeLessThanOrEqualTo(1));
            actions[0].X.ShouldBe(row.X);
            Math.Abs((actions[2].X + actions[2].Width) - (row.X + row.Width)).ShouldBeLessThanOrEqualTo(1);
        }

        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_plain_link_in_an_action_row_reads_as_a_primary_link()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, 1280);

        // Act
        var color = await page.Style("#top-link", "color");
        var decoration = await page.Style("#top-link", "text-decoration-line");

        // Assert
        color.ShouldBe(Primary);
        decoration.ShouldBe("underline");
    }
}
