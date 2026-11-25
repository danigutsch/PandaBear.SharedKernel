namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests in Chromium that an image grid or mosaic with a few photos fills its container, in a section's body, in a section's media
/// column and beside heading-less text, and that a lone tall photo stays within a sensible height. They skip when the browser is not
/// installed.
/// </summary>
[Testing.TestScope(Testing.TestScope.System)]
public sealed class ImageGridBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const double Gap = 16;

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
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

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
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

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
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

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
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

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(375, 3, false)]
    [InlineData(390, 3, true)]
    [InlineData(1280, 6, false)]
    [InlineData(1280, 6, true)]
    public async Task Thumbnails_crop_every_photo_to_one_square_in_small_columns_so_a_page_of_photos_stays_short(int width, int columns, bool tall)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, width, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = ImageGridLayout.Thumbnails,
            [nameof(GallerySample.Count)] = 24,
            [nameof(GallerySample.Tall)] = tall,
        });
        const string grid = ".site-section .site-image-grid";
        await page.ShowLoaded(grid);

        // Act
        var box = await page.Box(grid);
        var images = await page.Boxes(grid + " img");
        var fit = await page.Style(grid + " img", "object-fit");
        var scrollWidth = await page.ScrollWidth();

        // Assert
        fit.ShouldBe("cover");
        images.Select(image => image.X).Distinct().Count().ShouldBe(columns);
        images.ShouldAllSatisfy(image => Math.Abs(image.Height - image.Width).ShouldBeLessThanOrEqualTo(1));
        images.ShouldAllSatisfy(image => Math.Abs(image.Width - images[0].Width).ShouldBeLessThanOrEqualTo(1));
        images[0].X.ShouldBe(box.X);
        var rowEnd = images[columns - 1].X + images[columns - 1].Width;
        Math.Abs(rowEnd - (box.X + box.Width)).ShouldBeLessThanOrEqualTo(1);
        box.Height.ShouldBeLessThan(width < 768 ? 1100 : 900);
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(nameof(ImageGridLayout.Grid), nameof(SiteAlignment.Start), "0% 50%")]
    [InlineData(nameof(ImageGridLayout.Mosaic), nameof(SiteAlignment.Start), "0% 50%")]
    [InlineData(nameof(ImageGridLayout.Grid), nameof(SiteAlignment.Center), "50% 50%")]
    [InlineData(nameof(ImageGridLayout.Mosaic), nameof(SiteAlignment.Center), "50% 50%")]
    public async Task A_capped_photo_narrower_than_its_column_lines_up_with_the_text_of_its_section(string layout, string alignment, string position)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, 1280, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = Enum.Parse<ImageGridLayout>(layout),
            [nameof(GallerySample.Tall)] = true,
            [nameof(GallerySample.TextAlignment)] = Enum.Parse<SiteAlignment>(alignment),
        });
        const string grid = ".site-section .site-image-grid";
        await page.ShowLoaded(grid);

        // Act
        var objectPosition = await page.Style(grid + " img", "object-position");
        var headless = await page.Style("#headless .site-image-grid img", "object-position");

        // Assert
        objectPosition.ShouldBe(position);
        headless.ShouldBe("0% 50%");
    }

    [Theory(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_linked_thumbnail_is_a_tile_link_named_by_its_photo_with_a_focus_ring_around_the_tile(int width)
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, width, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = ImageGridLayout.Thumbnails,
            [nameof(GallerySample.Count)] = 6,
            [nameof(GallerySample.Linked)] = true,
        });
        const string grid = ".site-section .site-image-grid";
        await page.ShowLoaded(grid);

        // Act
        await page.Page.Keyboard.PressAsync("Tab");

        // Assert
        var focused = await page.Page.EvaluateAsync<string>("() => document.activeElement.getAttribute('href')");
        focused.ShouldBe("/full/1.jpg");
        var link = await page.Box(grid + " .site-image-grid__link");
        var image = await page.Box(grid + " img");
        link.ShouldBe(image);
        var middle = image.Y + (image.Height / 2);
        string[] ring =
        [
            await page.Pixel(image.X - 3.5, middle),
            await page.Pixel(image.X + image.Width + 3.5, middle),
            await page.Pixel(image.X + (image.Width / 2), image.Y - 3.5),
            await page.Pixel(image.X + (image.Width / 2), image.Bottom + 3.5),
        ];
        ring.ShouldAllSatisfy(pixel => pixel.ShouldBe("#1d4ed8"));
        var tree = await page.AccessibilityTree();
        tree.ShouldContain(node => node.Role == "link" && node.Name == "Photo 1");
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task The_thumbnail_sizes_cover_the_real_tile_width_without_overshooting_it_from_phone_to_desktop()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<GallerySample>(chromium.Browser, 1280, new Dictionary<string, object?>
        {
            [nameof(GallerySample.Layout)] = ImageGridLayout.Thumbnails,
            [nameof(GallerySample.Count)] = 24,
        });
        int[] widths = [320, 359, 360, 375, 414, 471, 472, 479, 480, 551, 583, 584, 600, 695, 696, 767, 768, 1000, 1183, 1184, 1280, 1600];
        var measured = new List<(int Width, double Tile, double Slot)>();

        // Act
        foreach (var width in widths)
        {
            await page.Page.SetViewportSizeAsync(width, 900);
            var tile = (await page.Box(".site-section .site-image-grid img")).Width;
            var slot = await page.Page.EvaluateAsync<double>(
                """
                (sizes) => {
                    for (const entry of sizes.split(',')) {
                        const match = entry.trim().match(/^(\(.*\))\s+(\S+)$/);
                        const [condition, length] = match ? [match[1], match[2]] : [null, entry.trim()];
                        if (condition === null || matchMedia(condition).matches) {
                            const probe = document.createElement('div');
                            probe.style.width = length;
                            document.body.append(probe);
                            const result = probe.getBoundingClientRect().width;
                            probe.remove();
                            return result;
                        }
                    }

                    return 0;
                }
                """,
                ImageGrid.ThumbnailSizes);
            measured.Add((width, tile, slot));
        }

        // Assert
        measured.ShouldAllSatisfy(entry => entry.Slot.ShouldBeGreaterThanOrEqualTo(entry.Tile - 0.5, $"at {entry.Width}px"));
        measured.ShouldAllSatisfy(entry => entry.Slot.ShouldBeLessThanOrEqualTo(entry.Tile * 1.6, $"at {entry.Width}px"));
    }
}
