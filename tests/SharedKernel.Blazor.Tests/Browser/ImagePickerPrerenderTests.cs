using PandaBear.Testing.AspNetCore;

namespace PandaBear.Blazor.Tests.Browser;

/// <summary>
/// Tests what a prerendered edit page with image pickers sends: the first chosen page only, in the markup and in the persisted state,
/// however many images are chosen, with every control disabled until the circuit starts.
/// </summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ImagePickerPrerenderTests
{
    [Fact]
    public async Task A_page_with_a_thousand_chosen_images_persists_and_renders_only_the_first_chosen_page()
    {
        // Arrange
        await using var host = await PickerHost.Start();
        using var client = new HttpClient { BaseAddress = host.BaseUri };
        var cancellation = Xunit.TestContext.Current.CancellationToken;

        // Act
        var firstPage = await client.GetStringAsync(host.Page(ImagePicker.DefaultChosenPageSize), cancellation);
        var thousand = await client.GetStringAsync(host.Page(PickerHost.LibrarySize), cancellation);

        // Assert
        var size = PrerenderedComponentState.ServerStateSize(thousand);
        size.ShouldBeGreaterThan(0);
        size.ShouldBe(PrerenderedComponentState.ServerStateSize(firstPage));
        PrerenderedComponentState.ShouldKeepServerStateBelow(thousand);
        thousand.ShouldContain("Photo 0024", StringComparison.Ordinal);
        thousand.ShouldNotContain("Photo 0025", StringComparison.Ordinal);
        thousand.ShouldContain("Showing 1 to 24 of 1000 chosen images.", StringComparison.Ordinal);
        thousand.ShouldContain("id=\"photos-find\" aria-expanded=\"false\" aria-controls=\"photos-panel\" aria-label=\"Find an image, Photos\" disabled", StringComparison.Ordinal);
        thousand.ShouldNotContain("id=\"photos-panel\"", StringComparison.Ordinal);
    }
}
