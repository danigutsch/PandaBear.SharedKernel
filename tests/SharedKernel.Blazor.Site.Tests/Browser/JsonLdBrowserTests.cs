using System.Text.Json.Nodes;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>Tests in Chromium that the JSON-LD script of a statically rendered page stays one script; they skip when the browser is not installed.</summary>
public sealed class JsonLdBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run sharedkernel-ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task Data_that_holds_a_closing_script_tag_stays_inside_one_script_that_the_browser_parses_back()
    {
        // Arrange
        const string hostile = "</script><script>window.injected = true</script><!--";
        var data = new JsonObject { ["@type"] = "TouristTrip", ["name"] = hostile };
        await using var page = await LayoutPage.Serve<JsonLd>(chromium.Browser, 1280, new Dictionary<string, object?> { [nameof(JsonLd.Data)] = data });

        // Act
        var scripts = await page.Page.EvaluateAsync<int>("() => document.querySelectorAll('script').length");
        var injected = await page.Page.EvaluateAsync<bool>("() => window.injected === true");
        var name = await page.Page.EvaluateAsync<string>("() => JSON.parse(document.querySelector('script[type=\"application/ld+json\"]').textContent).name");

        // Assert
        scripts.ShouldBe(1);
        injected.ShouldBeFalse();
        name.ShouldBe(hostile);
    }
}
