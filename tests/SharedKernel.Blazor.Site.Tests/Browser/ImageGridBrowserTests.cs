namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium that an image grid or mosaic with a few photos fills its container, in a section's body, in a section's media
/// column and beside heading-less text, and that a lone tall photo stays within a sensible height. They skip when the browser is not
/// installed.
/// </summary>
public sealed class ImageGridBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";
    private const double Gap = 16;

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(nameof(ImageGridLayout.Grid), 1, false)]
    [InlineData(nameof(ImageGridLayout.Grid), 2, false)]
    [InlineData(nameof(ImageGridLayout.Grid), 3, false)]
    [InlineData(nameof(ImageGridLayout.Mosaic), 1, false)]
    [InlineData(nameof(ImageGridLayout.Mosaic), 2, false)]
    [InlineData(nameof(ImageGridLayout.Mosaic), 3, false)]
    [InlineData(nameof(ImageGridLayout.Grid), 1, true)]
    [InlineData(nameof(ImageGridLayout.Grid), 2, true)]
    [InlineData(nameof(ImageGridLayout.Mosaic), 1, true)]
    [InlineData(nameof(ImageGridLayout.Mosaic), 2, true)]
    public async Task A_few_photos_share_the_full_width_of_their_container_in_as_many_columns(string layout, int count, bool inMedia)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, 1280, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = Enum.Parse<ImageGridLayout>(layout),
            [nameof(GallerySample.Count)] = count,
            [nameof(GallerySample.InMedia)] = inMedia,
        });
        const string grid = ".site-section .site-image-grid";
        await page.ShowLoaded(grid);

        // Act
        var container = await page.Box(inMedia ? ".site-section .site-split__media" : ".site-section__body");
        var box = await page.Box(grid);
        var figures = await page.Boxes(grid + " .site-image-grid__item");

        // Assert
        box.Width.ShouldBe(container.Width);
        var expected = (box.Width - ((count - 1) * Gap)) / count;
        figures.ShouldAllSatisfy(figure => Math.Abs(figure.Width - expected).ShouldBeLessThanOrEqualTo(1));
        figures.Select(figure => figure.X).Distinct().Count().ShouldBe(count);
        figures[0].X.ShouldBe(box.X);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(nameof(ImageGridLayout.Grid))]
    [InlineData(nameof(ImageGridLayout.Mosaic))]
    public async Task On_a_phone_two_photos_stack_at_the_full_width(string layout)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, 375, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = Enum.Parse<ImageGridLayout>(layout),
            [nameof(GallerySample.Count)] = 2,
        });
        const string grid = ".site-section .site-image-grid";
        await page.ShowLoaded(grid);

        // Act
        var box = await page.Box(grid);
        var figures = await page.Boxes(grid + " .site-image-grid__item");

        // Assert
        figures.ShouldAllSatisfy(figure => figure.Width.ShouldBe(box.Width));
        figures[1].Y.ShouldBeGreaterThan(figures[0].Y);
        var scrollWidth = await page.ScrollWidth();
        scrollWidth.ShouldBeLessThanOrEqualTo(375);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(nameof(ImageGridLayout.Grid))]
    [InlineData(nameof(ImageGridLayout.Mosaic))]
    public async Task A_lone_tall_photo_is_capped_in_height_and_shown_whole(string layout)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, 1280, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = Enum.Parse<ImageGridLayout>(layout),
            [nameof(GallerySample.Tall)] = true,
        });
        const string grid = ".site-section .site-image-grid";
        await page.ShowLoaded(grid);

        // Act
        var image = await page.Box(grid + " img");
        var fit = await page.Style(grid + " img", "object-fit");

        // Assert
        image.Height.ShouldBeLessThanOrEqualTo(640);
        image.Height.ShouldBeGreaterThan(600);
        fit.ShouldBe("contain");
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task Heading_less_text_and_photos_split_like_a_section_without_a_landmark(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, width, new Dictionary<string, object?> { [nameof(GallerySample.Count)] = 2 });
        await page.ShowLoaded("#headless");

        // Act
        var main = await page.Box("#headless .site-split__main");
        var media = await page.Box("#headless .site-split__media");
        var grid = await page.Box("#headless .site-image-grid");
        var tree = await page.AccessibilityTree();

        // Assert
        grid.Width.ShouldBe(media.Width);
        if (width < 768)
        {
            media.Y.ShouldBeGreaterThanOrEqualTo(main.Bottom);
            media.X.ShouldBe(main.X);
        }
        else
        {
            media.X.ShouldBeGreaterThanOrEqualTo(main.X + main.Width);
            media.Y.ShouldBeLessThan(main.Bottom);
            Math.Abs(media.Width - main.Width).ShouldBeLessThanOrEqualTo(1);
        }

        tree.Count(node => node.Role is "region" or "complementary").ShouldBe(0);
    }
}
