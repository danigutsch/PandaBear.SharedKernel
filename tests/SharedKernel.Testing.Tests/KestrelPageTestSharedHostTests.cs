using Microsoft.Extensions.DependencyInjection;
using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>
/// Tests that the browser tests of a class given a <see cref="KestrelHostFixture{TEntryPoint}"/> share its application: whichever test runs
/// second finds the application started once and still running after the first test ended.
/// </summary>
/// <param name="host">The shared application.</param>
public sealed class KestrelPageTestSharedHostTests(PingHostFixture host) : KestrelPageTest<Program>(host), IClassFixture<PingHostFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run dotnet ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public void Services_and_address_come_from_the_shared_application()
    {
        // Act
        var registered = Services.GetRequiredService<HostOwner>();

        // Assert
        registered.ShouldBeSameAs(host.Owner);
        BaseUri.ShouldBe(host.BaseAddress);
        host.Starts.ShouldBe(1);
    }

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_page_reaches_the_shared_application()
    {
        // Act
        await Page.GotoAsync(new Uri(BaseUri, "ping").AbsoluteUri);

        // Assert
        await Expect(Page.Locator("body")).ToHaveTextAsync("Staging|shared|hello from the app");
        host.Starts.ShouldBe(1);
    }
}
