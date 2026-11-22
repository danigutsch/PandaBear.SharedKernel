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
    [InlineData(5)]
    [InlineData(8)]
    public async Task On_a_phone_fact_cards_keep_the_auto_fit_columns_at_the_full_width(int count)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, 375, new Dictionary<string, object?> { [nameof(PartsSample.Cards)] = count });

        // Act
        var grid = await page.Box(".site-fact-grid");
        var cards = await page.Boxes(".site-fact-grid__item");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        cards.Count.ShouldBe(count);
        cards.ShouldAllSatisfy(card => card.X.ShouldBe(grid.X + Border));
        cards.ShouldAllSatisfy(card => Math.Abs(card.X + card.Width - (grid.X + grid.Width)).ShouldBeLessThanOrEqualTo(0.1));
        scrollWidth.ShouldBeLessThanOrEqualTo(375);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(4, new[] { 4 })]
    [InlineData(5, new[] { 3, 2 })]
    [InlineData(6, new[] { 3, 3 })]
    [InlineData(7, new[] { 4, 3 })]
    [InlineData(8, new[] { 4, 4 })]
    public async Task On_a_desktop_fact_cards_form_balanced_rows_that_each_fill_the_grid(int count, int[] rows)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, 1280, new Dictionary<string, object?> { [nameof(PartsSample.Cards)] = count });

        // Act
        var grid = await page.Box(".site-fact-grid");
        var cards = await page.Boxes(".site-fact-grid__item");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        cards.Count.ShouldBe(count);
        var byRow = cards.GroupBy(card => card.Y).ToArray();
        byRow.Select(row => row.Count()).ShouldBe(rows);
        foreach (var row in byRow)
        {
            var cells = row.ToArray();
            cells[0].X.ShouldBe(grid.X + Border);
            cells.ShouldAllSatisfy(card => Math.Abs(card.Width - cells[0].Width).ShouldBeLessThanOrEqualTo(1));
            cells.ShouldAllSatisfy(card => card.Height.ShouldBe(cells[0].Height));
            var rowEnd = cells[^1].X + cells[^1].Width;
            Math.Abs(rowEnd - (grid.X + grid.Width)).ShouldBeLessThanOrEqualTo(0.1);
        }

        scrollWidth.ShouldBeLessThanOrEqualTo(1280);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_phone_a_lone_last_fact_card_fills_its_row()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, 390, new Dictionary<string, object?> { [nameof(PartsSample.Cards)] = 7 });

        // Act
        var grid = await page.Box(".site-fact-grid");
        var cards = await page.Boxes(".site-fact-grid__item");

        // Assert
        cards.GroupBy(card => card.Y).Select(row => row.Count()).ShouldBe([2, 2, 2, 1]);
        cards[^1].X.ShouldBe(grid.X + Border);
        Math.Abs(cards[^1].X + cards[^1].Width - (grid.X + grid.Width)).ShouldBeLessThanOrEqualTo(0.1);
        Math.Abs(cards[1].X + cards[1].Width - (grid.X + grid.Width)).ShouldBeLessThanOrEqualTo(0.1);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_disclosure_after_other_content_keeps_its_distance_and_rounds_its_focus_ring_with_its_box(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width);
        await page.Page.FocusAsync(".site-fact-grid__value a");
        await page.Page.Keyboard.PressAsync("Tab");

        // Act
        var grid = await page.Box(".site-fact-grid");
        var disclosures = await page.Boxes(".site-disclosure");
        var boxRadius = await page.Style(".site-disclosure", "border-top-left-radius");
        var summaryRadius = await page.Style(".site-disclosure > summary", "border-top-left-radius");
        var outline = await page.Style(".site-disclosure > summary", "outline-style");

        // Assert
        var afterGrid = disclosures[0].Y - grid.Bottom;
        var between = disclosures[1].Y - disclosures[0].Bottom;
        Math.Abs(afterGrid - 24).ShouldBeLessThanOrEqualTo(0.2);
        Math.Abs(between - 8).ShouldBeLessThanOrEqualTo(0.2);
        summaryRadius.ShouldBe(boxRadius);
        summaryRadius.ShouldNotBe("0px");
        outline.ShouldBe("solid");
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_long_e_mail_value_wraps_only_before_its_at_sign_or_a_dot(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width, new Dictionary<string, object?> { [nameof(PartsSample.Cards)] = 5 });

        // Act
        var lines = await page.Page.EvaluateAsync<string[]>(
            """
            () => {
                const link = document.querySelector('.site-fact-grid__value a');
                const lines = [];
                let top = null;
                for (const node of link.childNodes) {
                    if (node.nodeType !== Node.TEXT_NODE) {
                        continue;
                    }

                    for (let index = 0; index < node.length; index++) {
                        const range = document.createRange();
                        range.setStart(node, index);
                        range.setEnd(node, index + 1);
                        const rect = range.getBoundingClientRect();
                        if (top === null || rect.top > top + 1) {
                            lines.push('');
                            top = rect.top;
                        }

                        lines[lines.length - 1] += node.data[index];
                    }
                }

                return lines;
            }
            """);

        // Assert
        string.Concat(lines).ShouldBe("reservations.and.small.groups@travel.example.org");
        lines.Skip(1).ShouldAllSatisfy(line => line[0].ShouldBeOneOf('.', '@'));
        if (width < 768)
        {
            lines.Length.ShouldBeGreaterThan(1);
        }
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
    [InlineData(375, nameof(ActionRowJustification.Start))]
    [InlineData(375, nameof(ActionRowJustification.Center))]
    [InlineData(375, nameof(ActionRowJustification.End))]
    [InlineData(375, nameof(ActionRowJustification.Between))]
    [InlineData(1280, nameof(ActionRowJustification.Start))]
    [InlineData(1280, nameof(ActionRowJustification.Center))]
    [InlineData(1280, nameof(ActionRowJustification.End))]
    [InlineData(1280, nameof(ActionRowJustification.Between))]
    public async Task An_action_row_places_its_actions_by_its_justification_and_stacks_them_on_a_phone(int width, string justification)
    {
        // Arrange
        var placement = Enum.Parse<ActionRowJustification>(justification);
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width, new Dictionary<string, object?> { [nameof(PartsSample.Justification)] = placement });

        // Act
        var row = await page.Box(".site-action-row");
        var actions = await page.Boxes(".site-action-row > *");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        actions.Count.ShouldBe(3);
        var rowEnd = row.X + row.Width;
        var rowCentre = row.X + (row.Width / 2);
        if (width < 768)
        {
            actions[1].Y.ShouldBeGreaterThanOrEqualTo(actions[0].Bottom);
            actions[2].Y.ShouldBeGreaterThanOrEqualTo(actions[1].Bottom);
            switch (placement)
            {
                case ActionRowJustification.Start:
                    actions.ShouldAllSatisfy(action => action.X.ShouldBe(row.X));
                    break;
                case ActionRowJustification.End:
                    actions.ShouldAllSatisfy(action => Math.Abs(action.X + action.Width - rowEnd).ShouldBeLessThanOrEqualTo(1));
                    break;
                default:
                    actions.ShouldAllSatisfy(action => Math.Abs(action.X + (action.Width / 2) - rowCentre).ShouldBeLessThanOrEqualTo(1));
                    break;
            }
        }
        else
        {
            actions.ShouldAllSatisfy(action => Math.Abs((action.Y + (action.Height / 2)) - (actions[0].Y + (actions[0].Height / 2))).ShouldBeLessThanOrEqualTo(1));
            var first = actions[0].X;
            var last = actions[2].X + actions[2].Width;
            var together = placement != ActionRowJustification.Between;
            if (together)
            {
                var firstGap = actions[1].X - (actions[0].X + actions[0].Width);
                var secondGap = actions[2].X - (actions[1].X + actions[1].Width);
                Math.Abs(firstGap - 16).ShouldBeLessThanOrEqualTo(0.2);
                Math.Abs(secondGap - 16).ShouldBeLessThanOrEqualTo(0.2);
            }

            switch (placement)
            {
                case ActionRowJustification.Start:
                    first.ShouldBe(row.X);
                    break;
                case ActionRowJustification.Center:
                    Math.Abs(((first + last) / 2) - rowCentre).ShouldBeLessThanOrEqualTo(1);
                    break;
                case ActionRowJustification.End:
                    Math.Abs(last - rowEnd).ShouldBeLessThanOrEqualTo(1);
                    break;
                default:
                    first.ShouldBe(row.X);
                    Math.Abs(last - rowEnd).ShouldBeLessThanOrEqualTo(1);
                    break;
            }
        }

        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(nameof(SiteAlignment.Start))]
    [InlineData(nameof(SiteAlignment.Center))]
    public async Task On_a_desktop_the_split_columns_start_at_the_top_or_are_centred_on_each_other(string alignment)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, 1280, new Dictionary<string, object?> { [nameof(PartsSample.SplitAlignment)] = Enum.Parse<SiteAlignment>(alignment) });
        await page.ShowLoaded("#day");

        // Act
        var main = await page.Box("#day .site-split__main");
        var media = await page.Box("#day .site-split__media");

        // Assert
        media.Height.ShouldBeGreaterThan(main.Height + 100);
        if (alignment == nameof(SiteAlignment.Start))
        {
            main.Y.ShouldBe(media.Y);
        }
        else
        {
            Math.Abs(main.Y + (main.Height / 2) - (media.Y + (media.Height / 2))).ShouldBeLessThanOrEqualTo(1);
        }
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_long_linked_fact_value_wraps_inside_its_card_as_a_primary_link(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<PartsSample>(chromium.Browser, width);

        // Act
        var cards = await page.Boxes(".site-fact-grid__item");
        var link = await page.Box(".site-fact-grid__value a");
        var color = await page.Style(".site-fact-grid__value a", "color");
        var decoration = await page.Style(".site-fact-grid__value a", "text-decoration-line");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        var card = cards[4];
        var linkEnd = link.X + link.Width;
        link.X.ShouldBeGreaterThanOrEqualTo(card.X);
        linkEnd.ShouldBeLessThanOrEqualTo(card.X + card.Width);
        color.ShouldBe(Primary);
        decoration.ShouldBe("underline");
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
