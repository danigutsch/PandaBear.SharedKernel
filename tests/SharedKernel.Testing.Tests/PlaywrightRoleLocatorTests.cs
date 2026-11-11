using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>Tests the exact-name role locators against real pages in Chromium; the tests skip when the browser is not installed.</summary>
public sealed class PlaywrightRoleLocatorTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task GetButton_matches_the_whole_name_and_not_a_longer_one()
    {
        // Arrange
        var page = await chromium.Show("""<button>Save draft</button><button>Save</button><button aria-label="save">S</button>""");

        // Act
        var count = await page.GetButton("Save").CountAsync();
        var text = await page.GetButton("Save").TextContentAsync();

        // Assert
        count.ShouldBe(1, "neither 'Save draft' nor a lower-case 'save' is the button named 'Save'");
        text.ShouldBe("Save");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task GetLink_matches_the_whole_name_and_not_a_longer_one()
    {
        // Arrange
        var page = await chromium.Show("""<a href="/tours/all">Tours and trips</a><a href="/tours">Tours</a>""");

        // Act
        var href = await page.GetLink("Tours").GetAttributeAsync("href");

        // Assert
        href.ShouldBe("/tours");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task GetHeading_matches_the_whole_name_and_optionally_the_level()
    {
        // Arrange
        var page = await chromium.Show("""<h1>Tours</h1><h2>Tours</h2><h2>Tours in Brazil</h2>""");

        // Act
        var anyLevel = await page.GetHeading("Tours").CountAsync();
        var second = await page.GetHeading("Tours", level: 2).EvaluateAsync<string>("element => element.tagName");

        // Assert
        anyLevel.ShouldBe(2);
        second.ShouldBe("H2");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task GetHeading_by_level_alone_finds_every_heading_of_that_level_whatever_its_name()
    {
        // Arrange
        var page = await chromium.Show("""<h1>Tours</h1><h2>Brazil</h2><h2>Chile</h2><div role="heading" aria-level="2">Peru</div>""");

        // Act
        var first = await page.GetHeading(1).TextContentAsync();
        var second = await page.GetHeading(2).CountAsync();

        // Assert
        first.ShouldBe("Tours");
        second.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void A_heading_level_alone_outside_one_to_six_is_refused(int level)
    {
        // Arrange
        var page = TitleAssertionPageProxy.Page;

        // Act
        Action locate = () => page.GetHeading(level);

        // Assert
        locate.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("level");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void A_heading_level_outside_one_to_six_is_refused(int level)
    {
        // Arrange
        var page = TitleAssertionPageProxy.Page;

        // Act
        Action locate = () => page.GetHeading("Tours", level);

        // Assert
        locate.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("level");
    }
}
