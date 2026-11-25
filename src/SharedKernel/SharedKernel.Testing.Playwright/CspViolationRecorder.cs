// Adapted from the ViajantesTurismo browser test fixture.
// Copyright (c) 2025 ViajantesTurismo contributors. Licensed under the MIT License.
using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace SharedKernel.Testing.Playwright;

/// <summary>Observes Content Security Policy events in the current document and policy messages on the page console.</summary>
public sealed class CspViolationRecorder : IDisposable
{
    private const string ObserveDocument = """
        globalThis.__pandaBearCspViolations = [];
        document.addEventListener('securitypolicyviolation', event =>
            globalThis.__pandaBearCspViolations.push(event.violatedDirective + ' ' + event.blockedURI));
        """;

    private readonly ConcurrentQueue<string> _console = new();
    private readonly IPage _page;

    private CspViolationRecorder(IPage page)
    {
        _page = page;
        _page.Console += ObserveConsole;
    }

    /// <summary>Installs the observer before a test navigates the page.</summary>
    /// <param name="page">The page to observe.</param>
    /// <returns>The recorder for the page.</returns>
    /// <exception cref="ArgumentNullException">The page is null.</exception>
    public static async Task<CspViolationRecorder> Start(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        await page.AddInitScriptAsync(ObserveDocument).ConfigureAwait(false);
        return new CspViolationRecorder(page);
    }

    /// <summary>Reads the current document's policy events followed by the policy console messages collected since recording started.</summary>
    /// <returns>The event and console messages, including both entries when the browser reports the same violation through both channels.</returns>
    public async Task<IReadOnlyList<string>> Violations()
    {
        var events = await _page.EvaluateAsync<string[]>("() => globalThis.__pandaBearCspViolations ?? []").ConfigureAwait(false);
        return [.. events, .. _console];
    }

    /// <summary>Reads the entries mentioning the <c>style-src</c> directive family.</summary>
    /// <returns>The matching event and console messages.</returns>
    public async Task<IReadOnlyList<string>> StyleViolations()
    {
        var entries = await Violations().ConfigureAwait(false);
        return [.. entries.Where(entry => entry.Contains("style-src", StringComparison.Ordinal))];
    }

    /// <summary>Detaches the console observer. The document listener follows the page lifetime.</summary>
    public void Dispose() => _page.Console -= ObserveConsole;

    private void ObserveConsole(object? sender, IConsoleMessage message)
    {
        if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
        {
            _console.Enqueue(message.Text);
        }
    }
}
