using Microsoft.Playwright;

namespace PandaBear.Blazor.Tests.Browser;

/// <summary>
/// Tests the image picker on an Interactive Server page in Chromium: the controls work once the circuit starts, and focus lands where a
/// keyboard user expects it after a removal, a move and a single choice. They skip when the browser is not installed. The picker
/// moves focus from <c>OnAfterRenderAsync</c>, a round trip after the browser applied the render, so the tests wait for the focus the
/// way they wait for the markup, instead of reading it once the markup changed.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ImagePickerBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Removing_and_moving_keep_focus_on_the_nearest_control_of_the_list()
    {
        // Arrange
        await using var host = await PickerHost.Start();
        var page = await chromium.Browser.OpenPicker(host, chosen: 3);

        // Act & Assert
        await page.PickerButton("photos", "Remove Photo 0002").ClickAsync();
        await Assertions.Expect(page.Locator("#photos ol li")).ToHaveCountAsync(2);
        await Assertions.Expect(page.PickerButton("photos", "Remove Photo 0003")).ToBeFocusedAsync();
        await page.PickerButton("photos", "Move Photo 0003 earlier").ClickAsync();
        await Assertions.Expect(page.Locator("#photos ol li span").First).ToHaveTextAsync("Photo 0003");
        await Assertions.Expect(page.PickerButton("photos", "Move Photo 0003 earlier")).ToBeFocusedAsync();
        await Assertions.Expect(page.Locator("#photos-status")).ToHaveTextAsync("Photo 0003 moved to position 1 of 2.");
        await page.PickerButton("photos", "Remove Photo 0003").ClickAsync();
        await page.PickerButton("photos", "Remove Photo 0001").ClickAsync();
        await Assertions.Expect(page.Locator("#photos .image-picker__empty")).ToHaveCountAsync(1);
        await Assertions.Expect(page.PickerButton("photos", "Find an image, Photos")).ToBeFocusedAsync();
        await page.CloseAsync();
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Choosing_a_single_image_closes_the_search_and_returns_focus_to_the_find_button()
    {
        // Arrange
        await using var host = await PickerHost.Start();
        var page = await chromium.Browser.OpenPicker(host, chosen: 0);
        await page.PickerButton("cover", "Find an image, Cover").ClickAsync();
        await Assertions.Expect(page.Locator("#cover-status")).ToHaveTextAsync("Showing the newest 24 of 1000 images.");
        await page.Locator("#cover-search").FillAsync("0500");
        await Assertions.Expect(page.Locator("#cover-status")).ToContainTextAsync("Showing 1 of 1 images that match ‘0500’.");

        // Act
        await page.PickerButton("cover", "Choose Photo 0500").ClickAsync();

        // Assert
        await Assertions.Expect(page.Locator("#cover ol li span")).ToHaveTextAsync("Photo 0500");
        await Assertions.Expect(page.Locator("#cover-panel")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("#cover-find")).ToHaveAttributeAsync("aria-expanded", "false");
        await Assertions.Expect(page.PickerButton("cover", "Find an image, Cover")).ToBeFocusedAsync();
        await page.CloseAsync();
    }
}
