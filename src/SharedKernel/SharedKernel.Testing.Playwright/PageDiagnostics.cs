using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace SharedKernel.Testing.Playwright;

/// <summary>
/// Records what a page's browser saw (console messages, page errors, failed requests, error responses, WebSocket lifecycle) so a wait that
/// times out can report what the browser and a Blazor circuit were doing. Create it right after the page, before navigating.
/// </summary>
public sealed class PageDiagnostics
{
    private const int MaxHtmlLength = 4000;
    private readonly ConcurrentQueue<string> _events = new();
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly IPage _page;

    /// <summary>Starts recording the events of a page.</summary>
    /// <param name="page">The page to observe.</param>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    public PageDiagnostics(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
        page.Console += (_, message) => Record($"console.{message.Type}: {message.Text}");
        page.PageError += (_, error) => Record($"pageerror: {error}");
        page.RequestFailed += (_, request) => Record($"requestfailed: {request.Method} {request.Url} {request.Failure}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                Record($"response {response.Status}: {response.Request.Method} {response.Url}");
            }
        };
        page.WebSocket += (_, socket) =>
        {
            Record($"websocket open: {socket.Url}");
            socket.SocketError += (_, error) => Record($"websocket error: {socket.Url} {error}");
            socket.Close += (_, _) => Record($"websocket close: {socket.Url}");
        };
    }

    /// <summary>Builds a report of the page address, the recorded events and the start of the page HTML (4000 characters).</summary>
    /// <returns>The report.</returns>
    public async Task<string> Describe()
    {
        var report = new StringBuilder();
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"url: {_page.Url}");
        foreach (var entry in _events)
        {
            _ = report.AppendLine(entry);
        }

        try
        {
            var html = await _page.ContentAsync().ConfigureAwait(false);
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"html ({html.Length} chars):");
            _ = report.AppendLine(html.Length > MaxHtmlLength ? html[..MaxHtmlLength] : html);
        }
        catch (PlaywrightException exception)
        {
            _ = report.AppendLine(CultureInfo.InvariantCulture, $"html unavailable: {exception.Message}");
        }

        return report.ToString();
    }

    private void Record(string entry) =>
        _events.Enqueue(string.Create(CultureInfo.InvariantCulture, $"+{_elapsed.Elapsed.TotalSeconds:F1}s {entry}"));
}
