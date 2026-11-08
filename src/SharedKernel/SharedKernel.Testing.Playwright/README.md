# SharedKernel.Testing.Playwright

[![Repository-wide build status](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/danigutsch/PandaBear.SharedKernel/actions/workflows/ci.yml)

Packages remain preview (prerelease); stable packages, tags, and releases require separate authorization.

Reusable Playwright assertions for SharedKernel browser test suites.

## Usage

Import `PandaBear.Testing.Playwright` and await `page.ShouldHaveTitle(expectedTitle)` in Playwright tests. Browser creation, readiness, navigation, and test authentication remain owned by the test fixture.

To state why the test expects the title, await `page.ShouldHaveTitle(expectedTitle, userMessage)`. Playwright puts the message before the failure and keeps its own exception type.

## Browser tests of a Blazor application

`KestrelPageTest<TEntryPoint>` is the base class for tests that run the application's real entry point on Kestrel and drive it with
Playwright's `PageTest` (`Page`, browser context). Each test starts its own application, so tests that use their own database stay
isolated. Override `EnvironmentName`, `Configuration` and `ConfigureServices` to arrange it and call `NavigateTo("customers/new")`, which
opens the page and waits until it is interactive.

```csharp
public abstract class AppBrowserTest : KestrelPageTest<WebMarker>
{
    protected override IReadOnlyDictionary<string, string?>? Configuration => new Dictionary<string, string?> { ["ConnectionStrings:Default"] = "..." };
}
```

Clicks and typing on a prerendered Blazor page are lost silently until the circuit connects. Render `<InteractiveMarker />` from
`SharedKernel.Blazor` once in the layout; `page.WaitForBlazorInteractive()` and `page.GotoInteractive(uri)` wait for it (they look for `data-interactive='true'`, exposed as
`PlaywrightPageExtensions.InteractiveReadySelector`; this package does not reference `SharedKernel.Blazor`, and a test in the Blazor test
project keeps the selector in sync). Blazor
commits text on the change event, so blur a field after `FillAsync`.

Playwright launches Chromium with `--no-sandbox` by default; Chromium's own sandbox is not needed for an application you start
yourself, and on Ubuntu 23.10+ AppArmor denies it to unprofiled binaries anyway. Install the browser binaries once with
`sharedkernel-ci install-playwright`; nothing here downloads them.

## Package identity and availability

Package ID: `PandaBear.SharedKernel.Testing.Playwright`. C# namespace: `PandaBear.Testing.Playwright`. Assembly/project identity: `SharedKernel.Testing.Playwright`.

Source is public; registry publication remains deferred. Consume a validated candidate from an explicitly configured local/private feed. This README does not imply availability on nuget.org.
