using Microsoft.Playwright;
using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the polling address wait against routed pages in Chromium; the browser tests skip when the browser is not installed.</summary>
public sealed class PlaywrightExpectPathTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    private static readonly Uri Origin = new("http://expect-path.test/");

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_navigation_that_completed_before_the_call_is_accepted()
    {
        // Arrange
        var page = await ExpectPathPages.Route(await chromium.Show(string.Empty), Origin, redirectDelayMilliseconds: null);
        _ = await page.GotoAsync(new Uri(Origin, "a").ToString());
        _ = await page.GotoAsync(new Uri(Origin, "b").ToString());

        // Act
        await page.ExpectPath(new Uri(Origin, "b"));

        // Assert
        page.Url.ShouldBe(new Uri(Origin, "b").ToString());
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_navigation_the_page_starts_itself_right_after_loading_is_seen()
    {
        // Arrange
        var page = await ExpectPathPages.Route(await chromium.Show(string.Empty), Origin, redirectDelayMilliseconds: 5);
        _ = await page.GotoAsync(new Uri(Origin, "a").ToString());

        // Act
        await page.ExpectPath(new Uri(Origin, "b"));

        // Assert
        page.Url.ShouldBe(new Uri(Origin, "b").ToString());
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_navigation_that_lands_after_the_call_is_awaited()
    {
        // Arrange
        var page = await ExpectPathPages.Route(await chromium.Show(string.Empty), Origin, redirectDelayMilliseconds: 300);
        _ = await page.GotoAsync(new Uri(Origin, "a").ToString());

        // Act
        await page.ExpectPath(new Uri(Origin, "b"));

        // Assert
        page.Url.ShouldBe(new Uri(Origin, "b").ToString());
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_relative_path_is_resolved_against_the_base_address()
    {
        // Arrange
        var page = await ExpectPathPages.Route(await chromium.Show(string.Empty), Origin, redirectDelayMilliseconds: null);
        _ = await page.GotoAsync(new Uri(Origin, "b").ToString());

        // Act
        await page.ExpectPath(Origin, "b");

        // Assert
        page.Url.ShouldBe(new Uri(Origin, "b").ToString());
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_non_ascii_path_is_compared_in_its_percent_encoded_form()
    {
        // Arrange
        var page = await ExpectPathPages.Route(await chromium.Show(string.Empty), Origin, redirectDelayMilliseconds: null);
        _ = await page.GotoAsync(new Uri(Origin, "caf%C3%A9").AbsoluteUri);

        // Act
        await page.ExpectPath(new Uri(Origin, "café"));

        // Assert
        page.Url.ShouldBe(new Uri(Origin, "caf%C3%A9").AbsoluteUri);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_page_that_stays_elsewhere_fails_and_names_the_expected_address()
    {
        // Arrange
        var page = await ExpectPathPages.Route(await chromium.Show(string.Empty), Origin, redirectDelayMilliseconds: null);
        _ = await page.GotoAsync(new Uri(Origin, "a").ToString());

        // Act
        Func<Task> expect = () => page.ExpectPath(new Uri(Origin, "b"));

        // Assert
        var exception = await expect.ShouldThrow<PlaywrightException>();
        exception.Message.ShouldContain(new Uri(Origin, "b").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_arguments_and_a_relative_expected_address_are_refused()
    {
        // Arrange
        var page = TitleAssertionPageProxy.Page;
        Func<Task> withoutPage = () => ((IPage)null!).ExpectPath(Origin);
        Func<Task> withoutAddress = () => page.ExpectPath(null!);
        Func<Task> withRelativeAddress = () => page.ExpectPath(new Uri("b", UriKind.Relative));
        Func<Task> withoutBase = () => page.ExpectPath(null!, "b");
        Func<Task> withRelativeBase = () => page.ExpectPath(new Uri("app/", UriKind.Relative), "b");
        Func<Task> withoutPath = () => page.ExpectPath(Origin, null!);

        // Act
        var missingPage = await withoutPage.ShouldThrow<ArgumentNullException>();
        var missingAddress = await withoutAddress.ShouldThrow<ArgumentNullException>();
        var relativeAddress = await withRelativeAddress.ShouldThrow<ArgumentException>();
        var missingBase = await withoutBase.ShouldThrow<ArgumentNullException>();
        var relativeBase = await withRelativeBase.ShouldThrow<ArgumentException>();
        var missingPath = await withoutPath.ShouldThrow<ArgumentNullException>();

        // Assert
        missingPage.ParamName.ShouldBe("page");
        missingAddress.ParamName.ShouldBe("expected");
        relativeAddress.ParamName.ShouldBe("expected");
        missingBase.ParamName.ShouldBe("baseAddress");
        relativeBase.ParamName.ShouldBe("baseAddress");
        missingPath.ParamName.ShouldBe("relativePath");
    }
}
