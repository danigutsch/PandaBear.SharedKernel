using Microsoft.Playwright;
using PandaBear.Testing.Playwright;

namespace PandaBear.Testing.Tests;

/// <summary>Exercises CSP observation against an enforced policy in Chromium.</summary>
public sealed class CspViolationRecorderTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    private const string Skip = "The Playwright Chromium browser is not installed; run dotnet ci install-playwright.";

    [Fact(SkipType = typeof(ChromiumHost), SkipUnless = nameof(ChromiumHost.IsInstalled), Skip = Skip)]
    public async Task The_recorder_observes_document_violations_and_policy_console_messages()
    {
        // Arrange
        var page = await chromium.Show(string.Empty);
        using var recorder = await CspViolationRecorder.Start(page);
        await page.RouteAsync("https://venue.invalid/*", async route => await route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "text/html",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Security-Policy"] = "style-src 'none'" },
            Body = "<!doctype html><html lang=\"en\"><title>Venue</title><style>h1 { color: red; }</style><h1>Venue</h1></html>",
        }));

        // Act
        var message = await page.RunAndWaitForConsoleMessageAsync(
            () => page.GotoAsync("https://venue.invalid/page"),
            new PageRunAndWaitForConsoleMessageOptions { Predicate = message => message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase) });
        var violations = await recorder.StyleViolations();

        // Assert
        violations.ShouldContain("style-src-elem inline");
        violations.ShouldContain(message.Text);
    }
}
