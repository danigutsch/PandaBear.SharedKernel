using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests that a browser test starts an application of its own by default and exposes the application's services.</summary>
public sealed class KestrelPageTestOwnHostTests : OwnHostPageTest
{
    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public void Services_belong_to_the_application_started_for_this_test()
    {
        // Act
        var registered = Services.GetRequiredService<HostOwner>();

        // Assert
        registered.ShouldBeSameAs(Owner);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task The_page_reaches_the_application_started_for_this_test()
    {
        // Act
        await Page.GotoAsync(new Uri(BaseUri, "ping").AbsoluteUri);

        // Assert
        await Expect(Page.Locator("body")).ToHaveTextAsync("Development|own|hello from the app");
    }
}
