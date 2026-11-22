namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests cards in Chromium, on a phone (375px) and a desktop (1280px) viewport: the more look beside a button link, actions at the bottom
/// across a row, a long word in a title, and clicks through the more look; they skip when the browser is not installed.
/// </summary>
public sealed class SiteCardsBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";
    private const double TapTarget = 44;

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375, ButtonSize.Regular, CardStyle.Boxed)]
    [InlineData(1280, ButtonSize.Regular, CardStyle.Plain)]
    [InlineData(1280, ButtonSize.Compact, CardStyle.Boxed)]
    public async Task The_more_look_has_exactly_the_size_of_a_button_link_with_the_same_text(int width, ButtonSize size, CardStyle style)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<CardsSample>(chromium.Browser, width, CardsSample.Parameters(size, style));

        // Act
        var more = await page.Box("#one .site-card__more");
        var button = await page.Box("#one .site-card__actions a.site-button");

        // Assert
        more.Width.ShouldBe(button.Width);
        more.Height.ShouldBe(button.Height);
        more.Y.ShouldBe(button.Y);
        more.Height.ShouldBeGreaterThanOrEqualTo(TapTarget);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(CardStyle.Boxed)]
    [InlineData(CardStyle.Plain)]
    public async Task Card_actions_line_up_at_the_bottom_of_a_row_of_cards_of_different_heights(CardStyle style)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<CardsSample>(chromium.Browser, 1280, CardsSample.Parameters(ButtonSize.Regular, style));

        // Act
        var cards = await page.Boxes("article.site-card");
        var texts = await page.Boxes(".site-card__body > p");
        var actions = await page.Boxes(".site-card__actions");

        // Assert
        cards.Select(card => card.Y).Distinct().Count().ShouldBe(1);
        texts[1].Height.ShouldBeGreaterThan(texts[0].Height);
        actions.Select(row => row.Y).Distinct().Count().ShouldBe(1);
        actions.Select(row => row.Bottom).Distinct().Count().ShouldBe(1);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_long_word_in_a_title_wraps_inside_its_card(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<CardsSample>(chromium.Browser, width, CardsSample.Parameters(ButtonSize.Regular, CardStyle.Boxed));

        // Act
        var cards = await page.Boxes("article.site-card");
        var title = await page.Box("#three .site-card__title a");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        var card = await page.Box("#three");
        title.X.ShouldBeGreaterThanOrEqualTo(card.X);
        var titleEnd = title.X + title.Width;
        titleEnd.ShouldBeLessThanOrEqualTo(card.X + card.Width);
        if (width > 768)
        {
            cards.Select(box => box.Width).Distinct().Count().ShouldBe(1);
        }

        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_click_on_the_more_look_reaches_the_card_link_and_a_click_on_the_button_link_reaches_it()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<CardsSample>(chromium.Browser, 1280, CardsSample.Parameters(ButtonSize.Regular, CardStyle.Boxed));
        var more = await page.Box("#one .site-card__more");
        var button = await page.Box("#one .site-card__actions a.site-button");

        // Act
        var underMore = await page.Page.EvaluateAsync<string>("([x, y]) => document.elementFromPoint(x, y).closest('a').getAttribute('href')", new[] { more.X + (more.Width / 2), more.Y + (more.Height / 2) });
        var underButton = await page.Page.EvaluateAsync<string>("([x, y]) => document.elementFromPoint(x, y).closest('a').getAttribute('href')", new[] { button.X + (button.Width / 2), button.Y + (button.Height / 2) });

        // Assert
        underMore.ShouldBe("/tours/one");
        underButton.ShouldBe("/sign-up/one");
    }
}
