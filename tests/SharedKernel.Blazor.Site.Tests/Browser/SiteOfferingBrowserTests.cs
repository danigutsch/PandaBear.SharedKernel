namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Tests the offering display atoms in Chromium at 1280px; they skip when the browser is not installed.</summary>
public sealed class SiteOfferingBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run dotnet ci install-playwright.";

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

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_inline_fact_grid_is_one_row_without_borders_in_a_card_of_three_to_a_row()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var items = await page.Boxes("#tour .site-fact-grid__item");
        var card = await page.Box("#tour");
        var border = await page.Style("#tour .site-fact-grid--inline", "border-top-width");
        var itemBorder = await page.Style("#tour .site-fact-grid__item", "border-bottom-width");

        // Assert
        items.Count.ShouldBe(3);
        items.Select(item => item.Y).Distinct().Count().ShouldBe(1);
        items.ShouldAllSatisfy(item => item.Right.ShouldBeLessThanOrEqualTo(card.Right));
        border.ShouldBe("0px");
        itemBorder.ShouldBe("0px");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_inline_fact_grid_wraps_inside_its_card_on_a_narrow_phone_instead_of_scrolling()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 320);

        // Act
        var items = await page.Boxes("#tour .site-fact-grid__item");
        var card = await page.Box("#tour");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        items.ShouldAllSatisfy(item => item.Right.ShouldBeLessThanOrEqualTo(card.Right));
        scrollWidth.ShouldBeLessThanOrEqualTo(320);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task A_price_amount_stays_on_one_line_inside_its_list_beside_a_long_label(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, width);

        // Act
        var amount = await page.Box("#price-list .site-price-list__amount data");
        var line = await page.Box("#price-list .site-price-list__note");
        var list = await page.Box("#price-list");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        amount.Height.ShouldBeLessThan(line.Height * 2);
        amount.Right.ShouldBeLessThanOrEqualTo(list.Right);
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_price_list_with_a_heading_ID_is_a_group_named_by_its_heading()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var tree = await page.AccessibilityTree();

        // Assert
        tree.ShouldContain(new AccessibleNode("group", "Prices", null));
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData("#price-list", "#ffffff")]
    [InlineData("#band-prices", "#1d4ed8")]
    public async Task A_price_note_reaches_4_5_to_1_on_the_page_and_on_the_primary_colour(string list, string background)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var note = ComputedColor.ToHex(await page.Style(list + " .site-price-list__note", "color"));
        var amount = ComputedColor.ToHex(await page.Style(list + " .site-price-list__amount", "color"));

        // Assert
        SiteFixtures.Contrast(note, background).ShouldBeGreaterThanOrEqualTo(4.5);
        SiteFixtures.Contrast(amount, background).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_from_price_keeps_its_amount_on_one_line_in_a_card()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 320);

        // Act
        var amount = await page.Box("#tour .site-from-price__amount");
        var weight = await page.Style("#tour .site-from-price__amount", "font-weight");
        var whiteSpace = await page.Style("#tour .site-from-price__amount", "white-space");

        // Assert
        amount.Height.ShouldBeLessThan(40);
        weight.ShouldBe("700");
        whiteSpace.ShouldBe("nowrap");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_step_list_is_a_list_of_its_steps_with_a_heading_each()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var listStyle = await page.Style("#itinerary", "list-style-type");
        var items = await page.Page.Locator("#itinerary > li").CountAsync();
        var headings = await page.Page.Locator("#itinerary").GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Level = 3 }).AllTextContentsAsync();
        var lists = await page.Page.GetByRole(Microsoft.Playwright.AriaRole.List).CountAsync();

        // Assert
        listStyle.ShouldBe("none");
        items.ShouldBe(3);
        headings.ShouldBe(["Valley of the Moon", "Valley of the Moon", "Valley of the Moon"]);
        lists.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task From_the_breakpoint_the_photos_of_an_itinerary_zigzag_and_start_beside_the_title()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var firstText = await page.Box("#itinerary > li:nth-child(1) .site-split__main");
        var firstMedia = await page.Box("#itinerary > li:nth-child(1) .site-split__media");
        var secondText = await page.Box("#itinerary > li:nth-child(2) .site-split__main");
        var secondMedia = await page.Box("#itinerary > li:nth-child(2) .site-split__media");

        // Assert
        firstMedia.X.ShouldBeGreaterThan(firstText.X);
        secondMedia.X.ShouldBeLessThan(secondText.X);
        firstMedia.Y.ShouldBe(firstText.Y);
        secondMedia.Y.ShouldBe(secondText.Y);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_phone_every_photo_of_an_itinerary_follows_its_text()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 375);

        // Act
        var secondText = await page.Box("#itinerary > li:nth-child(2) .site-split__main");
        var secondMedia = await page.Box("#itinerary > li:nth-child(2) .site-split__media");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        secondMedia.Y.ShouldBeGreaterThanOrEqualTo(secondText.Bottom);
        scrollWidth.ShouldBeLessThanOrEqualTo(375);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData("#itinerary", "#ffffff")]
    [InlineData("#band-itinerary", "#1d4ed8")]
    public async Task A_step_number_reaches_4_5_to_1_on_the_page_and_on_the_primary_colour(string list, string background)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var number = ComputedColor.ToHex(await page.Style(list + " .site-step-list__number", "color"));

        // Assert
        SiteFixtures.Contrast(number, background).ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData("#gallery", "#ffffff")]
    [InlineData("#band-gallery", "#1d4ed8")]
    public async Task An_image_credit_and_its_caption_reach_4_5_to_1_on_the_page_and_on_the_primary_colour(string gallery, string background)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var credit = ComputedColor.ToHex(await page.Style(gallery + " .site-image-credit", "color"));
        var link = ComputedColor.ToHex(await page.Style(gallery + " .site-image-credit a", "color"));
        var caption = ComputedColor.ToHex(await page.Style(gallery + " figcaption", "color"));
        var underline = await page.Style(gallery + " .site-image-credit a", "text-decoration-line");

        // Assert
        SiteFixtures.Contrast(credit, background).ShouldBeGreaterThanOrEqualTo(4.5);
        SiteFixtures.Contrast(link, background).ShouldBeGreaterThanOrEqualTo(4.5);
        SiteFixtures.Contrast(caption, background).ShouldBeGreaterThanOrEqualTo(4.5);
        underline.ShouldBe("underline");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_credited_image_link_keeps_its_alternative_text_as_its_name_and_the_credit_sits_under_the_caption()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, 1280);

        // Act
        var tree = await page.AccessibilityTree();
        var image = await page.Box("#gallery img");
        var credit = await page.Box("#gallery .site-image-credit");

        // Assert
        tree.ShouldContain(new AccessibleNode("link", "Salt flats at dusk", null));
        tree.ShouldContain(new AccessibleNode("link", "CC BY 4.0", null));
        tree.ShouldContain(new AccessibleNode("link", "Photo: Ana Lima", null));
        credit.Y.ShouldBeGreaterThan(image.Bottom);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(320)]
    [InlineData(1280)]
    public async Task A_credit_keeps_its_thumbnail_beside_its_lines_and_breaks_a_long_title_instead_of_scrolling(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<OfferingSample>(chromium.Browser, width);

        // Act
        var thumbnail = await page.Box("#credits .site-credits-list__thumbnail img");
        var text = await page.Box("#credits .site-credits-list__text");
        var list = await page.Box("#credits");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        thumbnail.Width.ShouldBe(80);
        text.X.ShouldBeGreaterThan(thumbnail.X + thumbnail.Width);
        text.Y.ShouldBe(thumbnail.Y);
        text.Right.ShouldBeLessThanOrEqualTo(list.Right);
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }
}
