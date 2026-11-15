using Microsoft.Extensions.DependencyInjection;

namespace PandaBear.Testing.Tests;

/// <summary>Tests that a browser test starts an application of its own by default and exposes the application's services.</summary>
public sealed class KestrelPageTestOwnHostTests : OwnHostPageTest
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public void Services_belong_to_the_application_started_for_this_test()
    {
        // Act
        var registered = Services.GetRequiredService<HostOwner>();

        // Assert
        registered.ShouldBeSameAs(Owner);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_page_reaches_the_application_started_for_this_test()
    {
        // Act
        await Page.GotoAsync(new Uri(BaseUri, "ping").AbsoluteUri);

        // Assert
        await Expect(Page.Locator("body")).ToHaveTextAsync("Development|own|hello from the app");
    }
}
