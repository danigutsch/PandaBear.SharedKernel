namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium the measure of section text, centred sections and bands, nested sections and the video link's poster; they skip
/// when the browser is not installed.
/// </summary>
public sealed class SiteSectionsBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData("#plain-text")]
    [InlineData("#plain .site-section__intro")]
    [InlineData("#centred-text")]
    [InlineData("#tagline-text")]
    public async Task On_a_desktop_a_paragraph_keeps_a_readable_measure_instead_of_the_content_width(string selector)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<SectionsSample>(chromium.Browser, 1280);

        // Act
        var paragraph = await page.Box(selector);
        var section = await page.Box("#plain");
        var characters = await page.Page.EvaluateAsync<double>(
            "(selector) => { const element = document.querySelector(selector); const probe = document.createElement('span'); probe.textContent = '0'.repeat(100); element.append(probe); const width = probe.getBoundingClientRect().width / 100; probe.remove(); return element.getBoundingClientRect().width / width; }",
            selector);

        // Assert
        paragraph.Width.ShouldBeLessThan(section.Width * 0.75);
        characters.ShouldBeLessThanOrEqualTo(65.5);
        characters.ShouldBeGreaterThanOrEqualTo(64.5);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_centred_section_centres_its_heading_divider_text_byline_and_video_link(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<SectionsSample>(chromium.Browser, width);

        // Act
        var section = await page.Box("#centred");
        var centre = section.X + (section.Width / 2);
        var parts = new[] { "#centred .site-divider", "#centred-text", "#centred .site-byline__text", "#centred .site-video-link" };
        var boxes = new List<ElementBox>();
        foreach (var part in parts)
        {
            boxes.Add(await page.Box(part));
        }

        var heading = await page.Style("#centred .site-section__title", "text-align");
        var text = await page.Style("#centred-text", "text-align");
        var plain = await page.Style("#plain-text", "text-align");

        // Assert
        heading.ShouldBe("center");
        text.ShouldBe("center");
        plain.ShouldBe("start");
        boxes.ShouldAllSatisfy(box => Math.Abs(box.X + (box.Width / 2) - centre).ShouldBeLessThanOrEqualTo(1));
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_centred_band_centres_its_text()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<SectionsSample>(chromium.Browser, 1280);

        // Act
        var band = await page.Box("#tagline");
        var text = await page.Box("#tagline-text");
        var align = await page.Style("#tagline-text", "text-align");

        // Assert
        align.ShouldBe("center");
        Math.Abs(text.X + (text.Width / 2) - (band.X + (band.Width / 2))).ShouldBeLessThanOrEqualTo(1);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task Nested_sections_take_the_width_of_the_outer_body_and_space_like_paragraph_groups(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<SectionsSample>(chromium.Browser, width);

        // Act
        var body = await page.Box("#itinerary .site-section__body");
        var first = await page.Box("#day-1");
        var second = await page.Box("#day-2");
        var firstTitle = await page.Box("#day-1 .site-section__title");
        var secondTitle = await page.Box("#day-2 .site-section__title");
        var outerHeader = await page.Box("#itinerary .site-section__header");

        // Assert
        first.X.ShouldBe(body.X);
        first.Width.ShouldBe(body.Width);
        second.Width.ShouldBe(body.Width);
        firstTitle.Y.ShouldBe(first.Y);
        var betweenDays = secondTitle.Y - first.Bottom;
        var underOuterHeading = firstTitle.Y - outerHeader.Bottom;
        Math.Abs(betweenDays - 40).ShouldBeLessThanOrEqualTo(0.2);
        Math.Abs(underOuterHeading - 24).ShouldBeLessThanOrEqualTo(0.2);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_video_poster_is_cropped_to_16_by_9_and_kept_to_the_video_width(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<SectionsSample>(chromium.Browser, width);
        await page.ShowLoaded("#centred .site-video-link");

        // Act
        var poster = await page.Box("#centred .site-video-link__poster img");
        var section = await page.Box("#centred");
        var fit = await page.Style("#centred .site-video-link__poster img", "object-fit");

        // Assert
        fit.ShouldBe("cover");
        Math.Abs(poster.Height - (poster.Width * 9 / 16)).ShouldBeLessThanOrEqualTo(1);
        poster.Width.ShouldBe(Math.Min(640, section.Width));
    }
}
