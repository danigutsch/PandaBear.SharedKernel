# SharedKernel.Testing.Playwright

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Playwright assertions, diagnostics and test bases for SharedKernel browser test suites.

## Usage

Import `SharedKernel.Testing.Playwright` and await `page.ShouldHaveTitle(expectedTitle)` in Playwright tests. Browser creation, readiness, navigation, and test authentication remain owned by the test fixture.

To state why the test expects the title, await `page.ShouldHaveTitle(expectedTitle, userMessage)`. Playwright puts the message before the failure and keeps its own exception type.

`page.GetHeading(name, level)`, `page.GetLink(name)` and `page.GetButton(name)` locate by role and exact accessible name; `page.GetHeading(level)` locates every heading of one level whatever its name. Playwright's
`GetByRole` matches a name as a case-insensitive substring by default, so `"Save"` would also find "Save draft".

## Structural accessibility checks

`PlaywrightAccessibilityExtensions` asserts common structural accessibility properties of the page in its current state. Each assertion
has an overload that takes a `userMessage` after its other arguments:

```csharp
await page.SetViewportSizeAsync(375, 800);
await NavigateTo("customers/new");
await page.ShouldHaveAccessibleStructure();      // named controls, links and buttons, one h1, no horizontal scroll
await page.ShouldHaveLanguage("en");
await page.ShouldHaveFocusWithin("[role='alert']", "A failed submit moves focus to the error summary.");
```

The single checks are `ShouldHaveNoUnlabelledControls`, `ShouldHaveNoUnnamedLinksOrButtons`, `ShouldHaveSingleLevelOneHeading`,
`ShouldHaveLanguage`, `ShouldNotScrollHorizontally` and `ShouldHaveFocusWithin`; `ShouldHaveAccessibleStructure` runs the first
two, the heading check and the scroll check and reports every problem in one failure. A failure throws `PlaywrightException` and names
the offending elements. The checks read the DOM once and do not wait: call them after the page is interactive and the state under test is
shown. The control, link and button checks skip elements that are not visible (`display: none`, `visibility: hidden`, the `hidden` attribute) and elements inside an `inert` or `aria-hidden="true"` subtree; the heading check skips an `h1` inside such a subtree too, so an inert live preview's `h1` is not a second page heading. They are a structural smoke test, not an audit: `aria-labelledby` is accepted when non-blank without resolving its target, a `title` is accepted as a name even though assistive
technology treats it as a weak one, `aria-label` on a descendant is accepted for a link or button without checking its role, and
`<input type="image">` is skipped rather than checked for `alt`. Colour contrast, focus order, landmarks, duplicate ids and the
accessible names of other roles are not checked; use an engine such as axe-core for those.

## IBM Equal Access audit

Use `ShouldHaveNoEqualAccessViolations` after the page reaches the state under test. Supply the
reviewed browser `ace.js` file and its pinned SHA-256 digest from the consuming test suite.

```csharp
await NavigateTo("gallery");
await page.ShouldHaveNoEqualAccessViolations(engineBundlePath, expectedSha256, "Gallery preview.");
```

The helper verifies the local file before running it in Playwright's evaluation scope. It leaves
the page's Content Security Policy in force and needs no script tag, CDN, or package download at
test time. Bundle acquisition, pin updates, and license notices belong to the consuming suite.

The scan uses the engine's `WCAG_2_2` rule set. Only definite violations fail the assertion. The
failure lists sorted, unique rule IDs. Page snippets, selectors, URLs, and engine errors stay out
of the failure. An unreadable or mismatched bundle or an invalid or empty report fails the test.
Manual review still covers criteria that an automated scan cannot assess.
[IBM Equal Access browser engine](https://github.com/IBMa/equal-access/blob/main-4.x/accessibility-checker-engine/README.md)

## Browser tests of a Blazor application

`KestrelPageTest<TEntryPoint>` is the base class for tests that run the application's real entry point on Kestrel and drive it with
Playwright's `PageTest` (`Page`, browser context). By default each test starts its own application, so tests that use their own database
stay isolated. Override `EnvironmentName`, `Configuration` and `ConfigureServices` to arrange it and call `NavigateTo("customers/new")`, which
opens the page and waits until it is interactive.

```csharp
public abstract class AppBrowserTest : KestrelPageTest<WebMarker>
{
    protected override IReadOnlyDictionary<string, string?>? Configuration => new Dictionary<string, string?> { ["ConnectionStrings:Default"] = "..." };
}
```

`Services` is the started application's root service provider, so a test can seed data or read a `TimeProvider` through the application's
own services; like `BaseUri`, it throws `InvalidOperationException` before the application starts.

A class whose tests only read can share one application instead of starting one per test: derive a `KestrelHostFixture<TEntryPoint>`,
arrange it with the same `EnvironmentName`, `Configuration` and `ConfigureServices` overrides (and `InitializeAsync`, to prepare a
database before calling the base), and pass it to the test's constructor as an xUnit class fixture:

```csharp
public sealed class PublicSiteHost : KestrelHostFixture<PublicMarker>
{
    protected override IReadOnlyDictionary<string, string?>? Configuration => new Dictionary<string, string?> { ["ConnectionStrings:Site"] = "..." };
}

public sealed class HomePageTests(PublicSiteHost host) : KestrelPageTest<PublicMarker>(host), IClassFixture<PublicSiteHost>
{
}
```

The fixture starts the application before the first test of the class and stops it after the last; each test still gets its own browser
context and page, and `BaseUri` and `Services` come from the fixture. The test's own `EnvironmentName`, `Configuration` and
`ConfigureServices` are then not used. The trade-off is isolation: every test of the class sees the database, caches and singletons the
others left behind, so share a host only when no test depends on another's absence. Per-test applications stay the default.

Clicks and typing on a prerendered Blazor page are lost silently until the circuit connects. Render `<InteractiveMarker />` from
`SharedKernel.Blazor` once in the layout; `page.WaitForBlazorInteractive()` and `page.GotoInteractive(uri)` wait for it (they look for `data-interactive='true'`, exposed as
`PlaywrightPageExtensions.InteractiveReadySelector`; this package does not reference `SharedKernel.Blazor`, and a test in the Blazor test
project keeps the selector in sync). Blazor
commits text on the change event, so blur a field after `FillAsync`.

`TracedPageTest` is the sibling for applications that already run elsewhere, such as an AppHost started by an
`AspireAppHostFixture` (`PandaBear.SharedKernel.IntegrationTesting`). Each test gets its own browser context, `PageDiagnostics` and a
trace; when the test fails, `PlaywrightArtifacts.Save` writes a full-page screenshot and the trace zip to `ArtifactsFolder`
(`TestResults/playwright` next to the test assembly; open the trace with `playwright show-trace`). `GotoInteractive(uri)` opens a Blazor
page with the diagnosing wait.

`PageDiagnostics` records console messages, page errors, failed requests, error responses and the WebSocket lifecycle of a page.
`page.WaitForBlazorInteractive(diagnostics, timeout)` reports the ASP.NET Core developer exception page as an `InvalidOperationException`
and any other failure as a `TimeoutException` carrying the diagnostics and the start of the HTML. `locator.FillAndExpectValue(value)`
fills, blurs and checks the value, filling again (at most three times) when an interactive render resets the field.

Playwright launches Chromium with `--no-sandbox` by default; Chromium's own sandbox is not needed for an application you start
yourself, and on Ubuntu 23.10+ AppArmor denies it to unprofiled binaries anyway. Install the browser binaries once with
`dotnet ci install-playwright`; nothing here downloads them.

## File inputs and policy observation

`locator.SetBlazorInputFile(file)` and `locator.SetBlazorInputFiles(files)` wait for Blazor to attach its change
listener before selecting files. They work with a rendered `InputFile`, including when interactivity is already visible
and the listener is still being installed. For a test that exercises the native chooser, await
`locator.WaitForBlazorInputFile()` before `page.RunAndWaitForFileChooserAsync` and keep the chooser action in the test.

Start `CspViolationRecorder.Start(page)` before navigation. `Violations()` returns policy events in the current document
followed by policy messages recorded on the page console since the recorder started. `StyleViolations()` selects
entries mentioning `style-src`. Dispose the recorder to detach its console observer. The document observer
follows the page lifetime.

The CSP recorder adapts a browser test fixture from ViajantesTurismo, copyright (c) 2025 ViajantesTurismo contributors,
under the MIT License.

## Chromium for component tests

Tests that render components without an application host share one Chromium per class through `ChromiumFixture`, an xUnit class
fixture, and skip when the browser is not installed:

```csharp
public sealed class CardBrowserTests(ChromiumFixture chromium) : IClassFixture<ChromiumFixture>
{
    [Fact(SkipType = typeof(Chromium), SkipUnless = nameof(Chromium.IsInstalled), Skip = Chromium.NotInstalledReason)]
    public async Task Card_is_one_column_on_a_phone()
    {
        // Arrange
        var html = await ComponentHtml.Render<Card>(new Dictionary<string, object?> { [nameof(Card.Heading)] = "Tour" });
        var page = await chromium.Browser.NewPageAsync();
        // ... set the content, act and assert ...
    }
}
```

`Chromium.IsInstalled` launches the browser once per test run to find out. When the environment variable
`REQUIRE_BROWSER_TESTS` (`Chromium.RequiredVariable`) is `true`, as in a CI run that installed Chromium on purpose, a failed
launch throws instead of skipping, so a missing or broken browser fails the run. `Chromium.Launch()` starts Playwright and a
headless Chromium without its own sandbox for a fixture of your own; the caller disposes both. `ComponentHtml.Render<TComponent>`
renders a component with its parameters as static server-side rendering does, with an empty service provider.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Playwright`. C# namespace: `SharedKernel.Testing.Playwright`. Assembly/project identity: `SharedKernel.Testing.Playwright`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
