namespace PandaBear.CI.Tool;

/// <summary>The Playwright browsers that a slice installs before its tests run.</summary>
/// <param name="Project">The test project of the slice whose build output holds <c>playwright.ps1</c>.</param>
/// <param name="Browsers">The browsers to install, such as <c>chromium</c>.</param>
/// <param name="OnlyShell">Whether to install only the headless shell of Chromium, not the full browser, for slices whose tests never run headed.</param>
internal sealed record PlaywrightConfiguration(string Project, string[] Browsers, bool OnlyShell = false);
