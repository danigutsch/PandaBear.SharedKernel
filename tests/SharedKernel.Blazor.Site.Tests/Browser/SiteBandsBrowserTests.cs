using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// Tests the collapsible menu, the header over a cover hero, the bands and the floating link in Chromium on a phone (375px) and a
/// desktop (1280px) viewport. The photos are all white, the worst photo for the white on-primary text, and the contrast is measured
/// on the rendered pixels. They skip when the browser is not installed.
/// </summary>
public sealed class SiteBandsBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_phone_the_menu_hides_the_navigation_and_actions_until_its_toggle_opens_them()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 375);
        var landmarksBefore = await page.NavigationLandmarks();
        var navigationBefore = await page.IsVisible(".site-nav");
        var actionBefore = await page.IsVisible("#action");
        var treeBefore = await page.AccessibilityTree();

        // Act
        await page.Page.ClickAsync(".site-header__toggle");

        // Assert
        treeBefore.ShouldContain(new AccessibleNode("DisclosureTriangle", "Menu", false));
        treeBefore.ShouldNotContain(node => node.Role == "navigation");
        var treeAfter = await page.AccessibilityTree();
        treeAfter.ShouldContain(new AccessibleNode("DisclosureTriangle", "Menu", true));
        treeAfter.Count(node => node.Role == "navigation").ShouldBe(1);
        landmarksBefore.ShouldBe(0);
        navigationBefore.ShouldBeFalse();
        actionBefore.ShouldBeFalse();
        var toggleVisible = await page.IsVisible(".site-header__toggle");
        toggleVisible.ShouldBeTrue();
        var landmarks = await page.NavigationLandmarks();
        landmarks.ShouldBe(1);
        var actionVisible = await page.IsVisible("#action");
        actionVisible.ShouldBeTrue();
        var toggle = await page.Box(".site-header__toggle");
        var brand = await page.Box(".site-header__brand");
        var navigation = await page.Box(".site-nav");
        toggle.Y.ShouldBeLessThan(brand.Bottom);
        navigation.Y.ShouldBeGreaterThanOrEqualTo(toggle.Bottom);
        var scrollWidth = await page.ScrollWidth();
        scrollWidth.ShouldBeLessThanOrEqualTo(375);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_phone_the_menu_toggle_opens_with_the_keyboard()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 375);
        await page.Page.FocusAsync("#brand");
        await page.Page.Keyboard.PressAsync("Tab");
        var toggleOutline = await page.Style(".site-header__toggle", "outline-style");

        // Act
        await page.Page.Keyboard.PressAsync("Enter");
        await page.Page.Keyboard.PressAsync("Tab");

        // Assert
        toggleOutline.ShouldBe("solid");
        var focused = await page.Page.EvaluateAsync<string>("() => document.activeElement.textContent");
        focused.ShouldBe("Home");
        var landmarks = await page.NavigationLandmarks();
        landmarks.ShouldBe(1);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_desktop_the_menu_shows_inline_without_a_toggle_and_one_navigation_landmark()
    {
        // Arrange & Act
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);

        // Assert
        var toggleVisible = await page.IsVisible(".site-header__toggle");
        toggleVisible.ShouldBeFalse();
        var tree = await page.AccessibilityTree();
        tree.Count(node => node.Role == "navigation").ShouldBe(1);
        tree.ShouldNotContain(node => node.Name == "Menu");
        var menuOpen = await page.Page.EvaluateAsync<bool>("() => document.querySelector('.site-header__menu').open");
        menuOpen.ShouldBeFalse();
        var landmarks = await page.NavigationLandmarks();
        landmarks.ShouldBe(1);
        var actionVisible = await page.IsVisible("#action");
        actionVisible.ShouldBeTrue();
        var brand = await page.Box(".site-header__brand");
        var items = await page.Boxes(".site-nav__list > .site-nav__item");
        var action = await page.Box("#action");
        items.ShouldAllSatisfy(item => item.Y.ShouldBeLessThan(brand.Bottom));
        items[0].X.ShouldBeGreaterThan(brand.X + brand.Width);
        action.X.ShouldBeGreaterThan(items[^1].X);
        await page.Page.FocusAsync("#brand");
        await page.Page.Keyboard.PressAsync("Tab");
        var focused = await page.Page.EvaluateAsync<string>("() => document.activeElement.textContent");
        focused.ShouldBe("Home");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_desktop_the_header_lies_over_the_cover_hero_and_the_scrim_fades_from_normal_to_large_text_contrast()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);
        await page.ShowLoaded(".site-hero");
        var header = await page.Box(".site-header");
        var hero = await page.Box(".site-hero");
        var title = await page.Box(".site-hero__title");
        var eyebrow = await page.Box(".site-hero__eyebrow");

        // Act
        var behindHeaderTop = await page.Pixel(2, 2);
        var behindHeaderBottom = await page.Pixel(2, header.Bottom - 2);
        var behindTitle = await page.Pixel(2, title.Y + (title.Height / 2));
        var heroBottom = await page.Pixel(2, hero.Bottom - 2);

        // Assert
        header.Y.ShouldBe(0);
        hero.Y.ShouldBe(0);
        eyebrow.Y.ShouldBeGreaterThanOrEqualTo(header.Bottom);
        var headerBackground = await page.Style(".site-header", "background-color");
        headerBackground.ShouldBe("rgba(0, 0, 0, 0)");
        var linkColor = await page.Style(".site-header .site-nav__item > a", "color");
        linkColor.ShouldBe("rgb(255, 255, 255)");
        var brandColor = await page.Style("#brand", "color");
        brandColor.ShouldBe("rgb(255, 255, 255)");
        var actionColor = await page.Style("#action", "color");
        actionColor.ShouldBe("rgb(255, 255, 255)");
        var titleColor = await page.Style(".site-hero__title", "color");
        titleColor.ShouldBe("rgb(255, 255, 255)");
        SiteFixtures.OnPrimaryContrast(behindHeaderTop).ShouldBeGreaterThanOrEqualTo(SiteScrim.TextMinimum);
        SiteFixtures.OnPrimaryContrast(behindHeaderBottom).ShouldBeGreaterThanOrEqualTo(SiteScrim.TextMinimum);
        SiteFixtures.OnPrimaryContrast(behindTitle).ShouldBeGreaterThanOrEqualTo(SiteScrim.LargeTextMinimum);
        SiteFixtures.OnPrimaryContrast(heroBottom).ShouldBeGreaterThanOrEqualTo(SiteScrim.LargeTextMinimum);
        SiteFixtures.OnPrimaryContrast(heroBottom).ShouldBeLessThan(SiteScrim.TextMinimum);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_desktop_the_outline_button_of_a_cover_hero_keeps_normal_text_contrast()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);
        await page.ShowLoaded(".site-hero");
        var button = await page.Box(".site-hero .site-button--outline");

        // Act
        var behindButton = await page.Pixel(button.X + 4, button.Y + 4);

        // Assert
        SiteFixtures.OnPrimaryContrast(behindButton).ShouldBeGreaterThanOrEqualTo(SiteScrim.TextMinimum);
        var buttonColor = await page.Style(".site-hero .site-button--outline", "color");
        buttonColor.ShouldBe("rgb(255, 255, 255)");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_desktop_a_cover_hero_without_a_photo_is_a_primary_band_under_the_header_without_a_scrim()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280, new Dictionary<string, object?> { [nameof(BandsSample.HeroImage)] = false });

        // Act
        var behindHeader = await page.Pixel(2, 2);
        var behindHero = await page.Pixel(2, 300);

        // Assert
        behindHeader.ShouldBe("#1d4ed8");
        behindHero.ShouldBe("#1d4ed8");
        (await page.Box(".site-hero")).Y.ShouldBe(0);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375, false, nameof(HeaderPlacement.OverHero), "rgb(29, 78, 216)", "rgb(255, 255, 255)")]
    [InlineData(1280, true, nameof(HeaderPlacement.OverHero), "rgb(29, 78, 216)", "rgb(255, 255, 255)")]
    [InlineData(1280, false, nameof(HeaderPlacement.Above), "rgb(255, 255, 255)", "rgb(31, 41, 55)")]
    public async Task The_header_stays_above_the_page_on_a_phone_before_other_content_and_when_placed_above(int width, bool leadText, string placement, string background, string linkColor)
    {
        // Arrange & Act
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, width, new Dictionary<string, object?>
        {
            [nameof(BandsSample.LeadText)] = leadText,
            [nameof(BandsSample.Placement)] = Enum.Parse<HeaderPlacement>(placement),
        });

        // Assert
        var header = await page.Box(".site-header");
        var hero = await page.Box(".site-hero");
        hero.Y.ShouldBeGreaterThanOrEqualTo(header.Bottom);
        var headerPosition = await page.Style(".site-header", "position");
        headerPosition.ShouldBe("static");
        var headerBackground = await page.Style(".site-header", "background-color");
        headerBackground.ShouldBe(background);
        var toggleColor = await page.Style(".site-header__toggle", "color");
        toggleColor.ShouldBe(linkColor);
        var brandColor = await page.Style("#brand", "color");
        brandColor.ShouldBe(placement == nameof(HeaderPlacement.OverHero) ? linkColor : "rgb(29, 78, 216)");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_phone_the_open_menu_of_a_header_placed_over_the_hero_is_on_the_primary_band()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 375);

        // Act
        await page.Page.ClickAsync(".site-header__toggle");

        // Assert
        var linkColor = await page.Style(".site-nav__item > a", "color");
        linkColor.ShouldBe("rgb(255, 255, 255)");
        var actionColor = await page.Style("#action", "color");
        actionColor.ShouldBe("rgb(255, 255, 255)");
        var behindLink = await page.Pixel(2, (await page.Box(".site-nav__item > a")).Y + 2);
        behindLink.ShouldBe("#1d4ed8");
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task On_a_desktop_the_header_over_the_hero_is_exactly_the_header_height_and_sizes_the_logo()
    {
        // Arrange & Act
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);
        await page.ShowLoaded(".site-header");

        // Assert
        var header = await page.Box(".site-header");
        var logo = await page.Box(".site-header__brand img");
        var item = await page.Box(".site-nav__list > .site-nav__item");
        header.Height.ShouldBe((double)(SiteTheme.Default.HeaderHeight * 16));
        logo.Height.ShouldBe((double)(SiteTheme.Default.BrandHeight * 16));
        var natural = Math.Max(logo.Height, item.Height) + 32;
        natural.ShouldBeLessThanOrEqualTo(header.Height);
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(1280, nameof(CoverHeight.Page), 550)]
    [InlineData(1280, nameof(CoverHeight.Landing), 765)]
    [InlineData(375, nameof(CoverHeight.Page), 320)]
    [InlineData(375, nameof(CoverHeight.Landing), 320)]
    public async Task A_cover_hero_is_at_least_as_tall_as_its_cover_height(int width, string height, int minimum)
    {
        // Arrange & Act
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, width, new Dictionary<string, object?>
        {
            [nameof(BandsSample.CoverHeight)] = Enum.Parse<CoverHeight>(height),
        });

        // Assert
        var hero = await page.Box(".site-hero");
        var minHeight = await page.Style(".site-hero", "min-height");
        minHeight.ShouldBe($"{minimum}px");
        hero.Height.ShouldBeGreaterThanOrEqualTo(minimum);
        if (width >= 768)
        {
            hero.Height.ShouldBe(minimum);
        }
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task An_image_band_darkens_its_photo_for_normal_text_and_a_primary_band_inverts_its_quote()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);
        await page.ShowLoaded(".site-band--image");
        var band = await page.Box(".site-band--image");

        // Act
        var behindText = await page.Pixel(2, band.Y + (band.Height / 2));

        // Assert
        SiteFixtures.OnPrimaryContrast(behindText).ShouldBeGreaterThanOrEqualTo(SiteScrim.TextMinimum);
        var bandTextColor = await page.Style("#band-text", "color");
        bandTextColor.ShouldBe("rgb(255, 255, 255)");
        band.Width.ShouldBe(1280);
        var primaryBackground = await page.Style(".site-band--primary", "background-color");
        primaryBackground.ShouldBe("rgb(29, 78, 216)");
        var quoteBackground = await page.Style(".site-band--primary .site-quote", "background-color");
        quoteBackground.ShouldBe("rgba(0, 0, 0, 0)");
        var citeColor = await page.Style(".site-band--primary .site-quote figcaption", "color");
        citeColor.ShouldBe("rgb(255, 255, 255)");
        var quoteColor = await page.Style(".site-band--primary .site-quote blockquote", "color");
        quoteColor.ShouldBe("rgb(255, 255, 255)");
        var mutedBackground = await page.Style(".site-band--muted", "background-color");
        mutedBackground.ShouldBe("rgb(243, 244, 246)");
    }

    [Theory(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    [InlineData(375)]
    [InlineData(1280)]
    public async Task A_split_section_puts_its_media_beside_the_text_on_a_desktop_and_under_it_on_a_phone(int width)
    {
        // Arrange & Act
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, width);
        await page.ShowLoaded(".site-section--split");

        // Assert
        var main = await page.Box(".site-section__main");
        var media = await page.Box(".site-section__media");
        if (width < 768)
        {
            media.Y.ShouldBeGreaterThanOrEqualTo(main.Bottom);
            media.X.ShouldBe(main.X);
        }
        else
        {
            media.X.ShouldBeGreaterThanOrEqualTo(main.X + main.Width);
            media.Y.ShouldBeLessThan(main.Bottom);
        }

        var scrollWidth = await page.ScrollWidth();
        scrollWidth.ShouldBeLessThanOrEqualTo(width);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task A_mosaic_flows_its_images_down_columns_without_cropping_them()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);
        await page.ShowLoaded(".site-image-grid--mosaic");

        // Act
        var figures = await page.Boxes(".site-image-grid--mosaic .site-image-grid__item");
        var cropped = await page.Page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('.site-image-grid--mosaic img')].filter(image => Math.abs(image.clientWidth / image.clientHeight - image.naturalWidth / image.naturalHeight) > 0.02 || getComputedStyle(image).objectFit !== 'fill').length");

        // Assert
        figures.Select(figure => figure.X).Distinct().Count().ShouldBeGreaterThan(1);
        figures[1].Y.ShouldBeGreaterThan(figures[0].Y);
        cropped.ShouldBe(0);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_floating_link_stays_in_the_corner_without_covering_the_end_of_the_page()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 375);

        // Act
        await page.Page.EvaluateAsync("() => window.scrollTo(0, document.documentElement.scrollHeight)");

        // Assert
        var link = await page.Box(".site-floating-link");
        var end = await page.Box("#footer-end");
        var position = await page.Style(".site-floating-link", "position");
        position.ShouldBe("fixed");
        link.Bottom.ShouldBeLessThanOrEqualTo(900);
        var linkRight = link.X + link.Width;
        linkRight.ShouldBeLessThanOrEqualTo(375);
        end.Bottom.ShouldBeLessThanOrEqualTo(link.Y);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_floating_link_shows_a_focus_ring_and_the_video_link_its_play_mark()
    {
        // Arrange
        await using var page = await LayoutPage.Serve<BandsSample>(chromium.Browser, 1280);
        await page.ShowLoaded(".site-video-link");
        await page.Page.FocusAsync("#footer-link");

        // Act
        await page.Page.Keyboard.PressAsync("Tab");

        // Assert
        var focused = await page.Page.EvaluateAsync<string>("() => document.activeElement.className");
        focused.ShouldBe("site-floating-link");
        var outline = await page.Style(".site-floating-link", "outline-style");
        outline.ShouldBe("solid");
        var poster = await page.Box(".site-video-link__poster");
        var play = await page.Box(".site-video-link__play");
        play.Width.ShouldBeGreaterThan(0);
        Math.Abs(play.X + (play.Width / 2) - (poster.X + (poster.Width / 2))).ShouldBeLessThanOrEqualTo(1);
        SiteFixtures.OnPrimaryContrast(await page.Pixel(play.X + 4, play.Y + (play.Height / 2))).ShouldBeGreaterThanOrEqualTo(SiteScrim.TextMinimum);
    }
}
