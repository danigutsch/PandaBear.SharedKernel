using Microsoft.Playwright;
using SharedKernel.Testing.Playwright;

namespace SharedKernel.Testing.Tests;

/// <summary>Tests the browser diagnostics, the diagnosing interactive wait, the retrying fill and the failure artifacts in Chromium; they skip when the browser is not installed.</summary>
public sealed class PlaywrightDiagnosticsTests(ChromiumPageFixture chromium) : IClassFixture<ChromiumPageFixture>
{
    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Diagnostics_describe_the_address_the_console_and_the_HTML()
    {
        // Arrange
        var page = await chromium.Show("<p id='text'>Hello</p>");
        var diagnostics = new PageDiagnostics(page);
        _ = await page.RunAndWaitForConsoleMessageAsync(() => page.EvaluateAsync("() => console.warn('circuit trouble')"));

        // Act
        var report = await diagnostics.Describe();

        // Assert
        report.ShouldContain("url: about:blank", StringComparison.Ordinal);
        report.ShouldContain("console.warning: circuit trouble", StringComparison.Ordinal);
        report.ShouldContain("<p id=\"text\">Hello</p>", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task An_interactive_wait_that_times_out_reports_the_browser_diagnostics()
    {
        // Arrange
        var page = await chromium.Show("<p>Never interactive</p>");
        var diagnostics = new PageDiagnostics(page);
        _ = await page.RunAndWaitForConsoleMessageAsync(() => page.EvaluateAsync("() => console.error('websocket refused')"));
        Func<Task> wait = () => page.WaitForBlazorInteractive(diagnostics, 300);

        // Act
        var timeout = await wait.ShouldThrow<TimeoutException>();

        // Assert
        timeout.Message.ShouldContain("never became interactive", StringComparison.Ordinal);
        timeout.Message.ShouldContain("console.error: websocket refused", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task An_interactive_wait_on_the_developer_exception_page_says_so()
    {
        // Arrange
        var page = await chromium.Show("<div id='stackpage'>Unhandled exception</div>");
        Func<Task> wait = () => page.WaitForBlazorInteractive(new PageDiagnostics(page), 300);

        // Act
        var failure = await wait.ShouldThrow<InvalidOperationException>();

        // Assert
        failure.Message.ShouldContain("developer exception page", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task An_interactive_page_passes_the_diagnosing_wait()
    {
        // Arrange
        var page = await chromium.Show("<span data-interactive='true'></span>");

        // Act
        await page.WaitForBlazorInteractive(new PageDiagnostics(page), 5000);

        // Assert
        await Microsoft.Playwright.Assertions.Expect(page.Locator(PlaywrightPageExtensions.InteractiveReadySelector)).ToHaveCountAsync(1);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task A_field_that_a_late_render_clears_is_filled_again_until_it_keeps_the_value()
    {
        // Arrange
        var page = await chromium.Show("""
            <input id="name" aria-label="Name">
            <script>
              let resets = 1;
              document.getElementById('name').addEventListener('change', e => { if (resets-- > 0) { e.target.value = ''; } });
            </script>
            """);

        // Act
        await page.Locator("#name").FillAndExpectValue("Ana");

        // Assert
        await Microsoft.Playwright.Assertions.Expect(page.Locator("#name")).ToHaveValueAsync("Ana");
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Failure_artifacts_are_a_full_page_screenshot_and_the_trace()
    {
        // Arrange
        var page = await chromium.Show("<h1>Failed here</h1>");
        await page.Context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
        var folder = Directory.CreateTempSubdirectory("sk-playwright-artifacts-").FullName;

        // Act
        var saved = await PlaywrightArtifacts.Save(page, folder, "My.Test(case: 1)");

        // Assert
        saved.ShouldBe([Path.Combine(folder, "My.Test_case__1_-19e03605.png"), Path.Combine(folder, "My.Test_case__1_-19e03605.zip")]);
        saved.ShouldAllSatisfy(file => new FileInfo(file).Length.ShouldBeGreaterThan(0), "both artifacts are written");
        Directory.Delete(folder, recursive: true);
    }

    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Artifact_names_are_capped_and_kept_apart_by_a_hash_of_the_full_name()
    {
        // Arrange
        var folder = Directory.CreateTempSubdirectory("sk-playwright-artifacts-").FullName;
        var longName = "Theory(" + new string('x', 400) + ")";
        var first = await chromium.Show("<h1>first</h1>");
        await first.Context.Tracing.StartAsync(new TracingStartOptions());
        var second = await chromium.Show("<h1>second</h1>");
        await second.Context.Tracing.StartAsync(new TracingStartOptions());

        // Act
        var savedLong = await PlaywrightArtifacts.Save(first, folder, longName);
        var savedLookAlike = await PlaywrightArtifacts.Save(second, folder, "My.Test(case_ 1)");

        // Assert
        savedLong.ShouldAllSatisfy(file => Path.GetFileName(file).Length.ShouldBeLessThanOrEqualTo(120), "long names are capped");
        savedLong.ShouldAllSatisfy(file => File.Exists(file).ShouldBeTrue(), "capped artifacts are written");
        savedLookAlike.ShouldBe([Path.Combine(folder, "My.Test_case__1_-cc6eefac.png"), Path.Combine(folder, "My.Test_case__1_-cc6eefac.zip")]);
        Directory.Delete(folder, recursive: true);
    }
}
