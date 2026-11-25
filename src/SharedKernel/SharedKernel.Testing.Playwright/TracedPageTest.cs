using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using Xunit;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// A Playwright test with its own browser context and <see cref="PageDiagnostics"/>, recording a trace; when the test fails, a full-page
/// screenshot and the trace are saved with <see cref="PlaywrightArtifacts"/> under <see cref="ArtifactsFolder"/>. For applications that
/// already run elsewhere, such as an Aspire AppHost started by a fixture; <see cref="KestrelPageTest{TEntryPoint}"/> runs the application
/// itself.
/// </summary>
public abstract class TracedPageTest : PageTest
{
    private PageDiagnostics? _diagnostics;

    /// <summary>Gets the folder failed tests save their screenshot and trace to. Default: <c>TestResults/playwright</c> next to the test assembly.</summary>
    protected virtual string ArtifactsFolder => Path.Combine(AppContext.BaseDirectory, "TestResults", "playwright");

    /// <summary>Gets the diagnostics of <c>Page</c>, recorded since the test started.</summary>
    /// <exception cref="InvalidOperationException">The test has not started yet.</exception>
    protected PageDiagnostics Diagnostics => _diagnostics ?? throw new InvalidOperationException("The test has not started.");

    /// <inheritdoc />
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync().ConfigureAwait(false);
        _diagnostics = new PageDiagnostics(Page);
        await Context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        try
        {
            if (TestContext.Current.TestState?.Result == TestResult.Failed)
            {
                var saved = await PlaywrightArtifacts.Save(Page, ArtifactsFolder, TestContext.Current.Test?.TestDisplayName ?? "test").ConfigureAwait(false);
                TestContext.Current.SendDiagnosticMessage("Playwright artifacts: " + string.Join(", ", saved));
            }
            else
            {
                await Context.Tracing.StopAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await base.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Opens a Blazor page and waits until it is interactive; a page that never gets there fails with the browser diagnostics.</summary>
    /// <param name="address">The address.</param>
    /// <returns>A task that completes when the page is interactive.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is <see langword="null"/>.</exception>
    protected async Task GotoInteractive(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _ = await Page.GotoAsync(address.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }).ConfigureAwait(false);
        await Page.WaitForBlazorInteractive(Diagnostics).ConfigureAwait(false);
    }
}
