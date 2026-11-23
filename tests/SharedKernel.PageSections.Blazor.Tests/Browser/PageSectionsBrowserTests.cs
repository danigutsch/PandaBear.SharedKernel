using Microsoft.Playwright;
using static PandaBear.PageSections.Blazor.Tests.SectionFixtures;

namespace PandaBear.PageSections.Blazor.Tests.Browser;

/// <summary>
/// Tests rendered sections in Chromium on a phone (375px) and a desktop (1280px) viewport: what assistive technology is told, where the
/// images beside the text lie, and the centred alignment. They skip when the browser is not installed.
/// </summary>
public sealed class PageSectionsBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run dotnet ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_section_is_a_region_named_by_its_heading_its_questions_open_on_demand_and_its_links_are_named()
    {
        // Arrange
        await using var page = await SectionsPage.Open(chromium.Browser, 1280, [Rich()]);
        var answer = page.Page.GetByText("Alugamos uma para você.", new PageGetByTextOptions { Exact = true });
        var hiddenBefore = await answer.IsHiddenAsync();

        // Act
        await page.Page.GetByText("Preciso de bicicleta?", new PageGetByTextOptions { Exact = true }).ClickAsync();

        // Assert
        hiddenBefore.ShouldBeTrue();
        var shown = await answer.IsVisibleAsync();
        shown.ShouldBeTrue();
        var regions = await page.Page.GetByRole(AriaRole.Region, new PageGetByRoleOptions { Name = "Explore a China", Exact = true }).CountAsync();
        regions.ShouldBe(1);
        var heading = await page.Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Explore a China", Exact = true, Level = 2 }).CountAsync();
        heading.ShouldBe(1);
        var video = await page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Nossa viagem (abre em youtube.com)", Exact = true }).CountAsync();
        video.ShouldBe(1);
        var button = await page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Saiba mais", Exact = true }).GetAttributeAsync("href");
        button.ShouldBe("/pt/viagens");
        var poster = await page.Page.Locator(".site-video-link img").GetAttributeAsync("alt");
        poster.ShouldBe(string.Empty);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375, false)]
    [InlineData(1280, true)]
    public async Task Images_beside_the_text_stand_beside_it_on_a_wide_screen_and_follow_it_on_a_phone(int width, bool beside)
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Heading = "Quem somos", Text = "Somos uma agência.", ImageIds = [Photo], ImageLayout = SectionImageLayout.BesideText }),
            Plain(new() { Text = "Viaje diferente!", ImageIds = [2], ImageLayout = SectionImageLayout.BesideText }),
        ];

        // Act
        await using var page = await SectionsPage.Open(chromium.Browser, width, sections);

        // Assert
        foreach (var band in new[] { 1, 2 })
        {
            var text = await page.Box($".site-band:nth-of-type({band}) .site-split__main");
            var media = await page.Box($".site-band:nth-of-type({band}) .site-split__media");
            if (beside)
            {
                media.X.ShouldBeGreaterThanOrEqualTo(text.X + text.Width);
                media.Y.ShouldBe(text.Y);
            }
            else
            {
                media.Y.ShouldBeGreaterThanOrEqualTo(text.Y + text.Height);
                media.X.ShouldBe(text.X);
            }
        }
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Centred_bands_centre_the_heading_the_text_and_the_button()
    {
        // Arrange
        PageSection<int, string, NoExtra>[] sections =
        [
            Plain(new() { Heading = "Quem somos", Text = "Somos uma agência.", Link = new PageLinkDefinition<string>("Saiba mais", "sobre") }),
        ];

        // Act
        await using var page = await SectionsPage.Open(chromium.Browser, 1280, sections, SiteAlignment.Center);

        // Assert
        var heading = await page.Style(".site-section__title", "text-align");
        heading.ShouldBe("center");
        var text = await page.Style(".site-section__body > p", "text-align");
        text.ShouldBe("center");
        var band = await page.Box(".site-band__inner");
        var button = await page.Box(".site-button");
        var bandCentre = band.X + (band.Width / 2);
        var buttonCentre = button.X + (button.Width / 2);
        Math.Abs(bandCentre - buttonCentre).ShouldBeLessThan(1);
    }
}
