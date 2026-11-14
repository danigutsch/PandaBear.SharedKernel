using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Playwright;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>A browser page on the <see cref="PreviewHost" /> that records every Content-Security-Policy violation the browser reports.</summary>
internal sealed class PreviewPage : IAsyncDisposable
{
    private const string RecordViolations = """
        window.cspViolations = [];
        document.addEventListener('securitypolicyviolation', e => window.cspViolations.push(e.violatedDirective + ' ' + e.blockedURI));
        """;

    private readonly ConcurrentQueue<string> _console;

    private PreviewPage(IPage page, string nonce, ConcurrentQueue<string> console)
    {
        Page = page;
        Nonce = nonce;
        _console = console;
        page.PageError += (_, error) => _console.Enqueue(error);
    }

    /// <summary>Gets the page.</summary>
    public IPage Page { get; }

    /// <summary>Gets the nonce of the page's response.</summary>
    public string Nonce { get; }

    /// <summary>Opens the preview and waits until its circuit is interactive.</summary>
    /// <param name="browser">The browser.</param>
    /// <param name="host">The host.</param>
    /// <returns>The page.</returns>
    public static async Task<PreviewPage> Open(IBrowser browser, PreviewHost host)
    {
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ColorScheme = ColorScheme.Light });
        await page.AddInitScriptAsync(RecordViolations);
        var console = new ConcurrentQueue<string>();
        page.Console += (_, message) => console.Enqueue(message.Text);
        page.Response += (_, answer) =>
        {
            if (answer.Status >= 400)
            {
                console.Enqueue($"{answer.Status} {answer.Url}");
            }
        };
        var response = await page.GotoAsync(host.BaseUri.ToString()) ?? throw new InvalidOperationException("The page did not answer.");
        var policy = await response.HeaderValueAsync("content-security-policy") ?? throw new InvalidOperationException("The page has no policy.");
        var nonce = policy.Split("'nonce-", 2)[1].Split('\'', 2)[0];
        var preview = new PreviewPage(page, nonce, console);
        try
        {
            await Assertions.Expect(page.Locator("main[data-interactive=\"true\"]")).ToHaveCountAsync(1);
        }
        catch (PlaywrightException exception)
        {
            throw new InvalidOperationException($"The page never became interactive. Console: {string.Join(" | ", preview._console)}", exception);
        }

        return preview;
    }

    /// <summary>Gets the CSP violations from the page's event and the console.</summary>
    /// <returns>The violations.</returns>
    public async Task<IReadOnlyList<string>> Violations()
    {
        var events = await Page.EvaluateAsync<string[]>("() => window.cspViolations");
        return [.. events, .. _console.Where(text => text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Gets the computed colour of the preview's first heading.</summary>
    /// <returns>The colour, such as <c>rgb(31, 41, 55)</c>.</returns>
    public Task<string> HeadingColor() => Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#preview h1')).color");

    /// <summary>Gets the nonce in the preview style element's internal slot, which a browser hides from the content attribute.</summary>
    /// <returns>The nonce.</returns>
    public Task<string> StyleNonce() => Page.EvaluateAsync<string>("() => document.querySelector('#preview style').nonce");

    /// <summary>Remembers the current preview style element, to tell later whether a re-render kept or replaced it.</summary>
    /// <returns>A task that completes when the element is remembered.</returns>
    public Task MarkStyle() => Page.EvaluateAsync("() => { window.markedStyle = document.querySelector('#preview style'); }");

    /// <summary>Gets whether the preview style element is still the remembered one.</summary>
    /// <returns><see langword="true" /> when it is the same element.</returns>
    public Task<bool> StyleIsMarked() => Page.EvaluateAsync<bool>("() => window.markedStyle === document.querySelector('#preview style')");

    /// <summary>Changes the text colour through the circuit and waits until the style element has the new text.</summary>
    /// <returns>A task that completes when the style has changed.</returns>
    public async Task ChangeText()
    {
        await Page.ClickAsync("#change");
        _ = await Page.WaitForFunctionAsync(
            "text => document.querySelector('#preview style').textContent.includes(text)",
            PreviewSample.ChangedText);
    }

    /// <summary>Replaces the preview through the circuit and waits until the new one is rendered.</summary>
    /// <returns>A task that completes when the preview has been replaced.</returns>
    public async Task ReplacePreview()
    {
        await Page.ClickAsync("#replace");
        await Assertions.Expect(Page.Locator("main[data-generation=\"1\"]")).ToHaveCountAsync(1);
    }

    /// <summary>Converts a <c>#rrggbb</c> colour to the computed-style form.</summary>
    /// <param name="hex">The colour.</param>
    /// <returns>The colour as <c>rgb(r, g, b)</c>.</returns>
    public static string Rgb(string hex)
    {
        var value = Convert.FromHexString(hex.TrimStart('#'));
        return string.Create(CultureInfo.InvariantCulture, $"rgb({value[0]}, {value[1]}, {value[2]})");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => new(Page.CloseAsync());
}
