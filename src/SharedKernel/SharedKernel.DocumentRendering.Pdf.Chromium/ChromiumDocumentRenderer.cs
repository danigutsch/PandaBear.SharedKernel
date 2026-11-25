using System.Text;
using Microsoft.Playwright;
using SharedKernel.InputNormalization;

namespace SharedKernel.DocumentRendering.Pdf.Chromium;

/// <summary>Renders structured documents as bounded, deterministic tagged PDFs in a borrowed Chromium browser.</summary>
public sealed class ChromiumDocumentRenderer
{
    private const int MaxEmbeddedLogoLength = 60_000;
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    private readonly IBrowser _browser;
    private readonly QpdfDocumentNormalizer _normalizer;
    private readonly int _maxHtmlBytes;
    private readonly int _maxPdfBytes;
    private readonly TimeSpan _timeout;

    /// <summary>Creates a renderer for caller-provisioned Chromium and qpdf runtimes.</summary>
    /// <param name="browser">A Chromium browser whose process and configuration remain caller-owned.</param>
    /// <param name="qpdfExecutablePath">A fully-qualified path to the pinned qpdf executable.</param>
    /// <param name="maxHtmlBytes">The maximum UTF-8 HTML size submitted to Chromium.</param>
    /// <param name="maxPdfBytes">The maximum PDF size at both transfer and normalization boundaries.</param>
    /// <param name="timeout">The operation deadline, excluding up to five seconds for browser context cleanup.</param>
    /// <exception cref="ArgumentNullException"><paramref name="browser"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="qpdfExecutablePath"/> is empty or not fully qualified.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size limit or the timeout is outside its supported range.</exception>
    public ChromiumDocumentRenderer(IBrowser browser, string qpdfExecutablePath, int maxHtmlBytes, int maxPdfBytes, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHtmlBytes);
        _normalizer = new QpdfDocumentNormalizer(qpdfExecutablePath, maxPdfBytes, timeout);
        _browser = browser;
        _maxHtmlBytes = maxHtmlBytes;
        _maxPdfBytes = maxPdfBytes;
        _timeout = timeout;
    }

    /// <summary>Renders a structured request and normalizes the generated PDF dates.</summary>
    /// <param name="request">The trusted structured input, with any logo embedded as a bounded raster data URI.</param>
    /// <param name="documentDate">The caller's immutable document date.</param>
    /// <param name="ct">Cancels the render and closes its private browser context.</param>
    /// <returns>The bounded normalized PDF bytes.</returns>
    /// <exception cref="ArgumentNullException">The request or a required member is null.</exception>
    /// <exception cref="InvalidDataException">The HTML or PDF is outside its bounds, an asset is unavailable offline, or the PDF does not pass normalization.</exception>
    /// <exception cref="InvalidOperationException">A structured field is unclassified or the qpdf process cannot run within its capture limits.</exception>
    /// <exception cref="PlaywrightException">Chromium is unavailable or rejects the browser operation.</exception>
    /// <exception cref="IOException">The private normalization files could not be written or cleaned up.</exception>
    /// <exception cref="TimeoutException">The rendering or cleanup deadline elapsed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public async Task<ReadOnlyMemory<byte>> Render(DocumentRenderRequest request, DateTimeOffset documentDate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(_timeout);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var logo = request.Branding?.LogoUri;
            if (logo is not null && WebAssetUriSanitizer.NormalizeRasterDataImage(logo.OriginalString, MaxEmbeddedLogoLength) is null)
            {
                throw new InvalidDataException("The offline PDF branding logo must be an embedded raster image.");
            }

            var html = HtmlDocumentRenderer.Render(request, _maxHtmlBytes, operation.Token);
            var raw = await Print(request.Language, Encoding.UTF8.GetString(html), logo is not null, operation.Token).ConfigureAwait(false);
            return await _normalizer.Normalize(raw, documentDate, operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Document PDF rendering exceeded its time limit.");
        }
    }

    private async Task<ReadOnlyMemory<byte>> Print(string locale, string html, bool hasLogo, CancellationToken ct)
    {
        var context = await OpenContext(locale, ct).ConfigureAwait(false);
        return await PrintAndClose(() => PrintInContext(context, html, hasLogo, ct), () => CloseContext(context))
            .ConfigureAwait(false);
    }

    /// <summary>Closes the renderer's private context before publishing the printed bytes.</summary>
    internal static async Task<ReadOnlyMemory<byte>> PrintAndClose(Func<Task<ReadOnlyMemory<byte>>> print, Func<Task> close)
    {
        ReadOnlyMemory<byte> pdf;
        try
        {
            pdf = await print().ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await close().ConfigureAwait(false);
            }
            catch (Exception cleanupException) when (cleanupException is PlaywrightException or TimeoutException)
            {
                // Preserve the primary render failure.
            }

            throw;
        }

        await close().ConfigureAwait(false);
        return pdf;
    }

    private async Task<ReadOnlyMemory<byte>> PrintInContext(IBrowserContext context, string html, bool hasLogo, CancellationToken ct)
    {
        var attemptedRequest = 0;
        await context.RouteAsync("**/*", async route =>
        {
            Interlocked.Exchange(ref attemptedRequest, 1);
            await route.AbortAsync().ConfigureAwait(false);
        }).WaitAsync(ct).ConfigureAwait(false);

        var page = await context.NewPageAsync().WaitAsync(ct).ConfigureAwait(false);
        await page.SetContentAsync(html).WaitAsync(ct).ConfigureAwait(false);
        RequireOffline(Volatile.Read(ref attemptedRequest));
        if (hasLogo)
        {
            var loaded = await page.Locator("img").EvaluateAllAsync<bool>(
                "images => images.length === 1 && images[0].complete && images[0].naturalWidth > 0")
                .WaitAsync(ct).ConfigureAwait(false);
            if (!loaded)
            {
                throw new InvalidDataException("Chromium could not decode the embedded branding logo.");
            }
        }

        var session = await context.NewCDPSessionAsync(page).WaitAsync(ct).ConfigureAwait(false);
        var pdf = await ChromiumPdfTransfer.Print(session, _maxPdfBytes, ct).ConfigureAwait(false);
        RequireOffline(Volatile.Read(ref attemptedRequest));
        return pdf;
    }

    private async Task<IBrowserContext> OpenContext(string locale, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var opening = _browser.NewContextAsync(new()
        {
            Locale = locale,
            Offline = true,
            JavaScriptEnabled = false,
            ServiceWorkers = ServiceWorkerPolicy.Block,
        });

        try
        {
            return await opening.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = CloseLateContext(opening);
            throw;
        }
    }

    private static async Task CloseLateContext(Task<IBrowserContext> opening)
    {
        try
        {
            var context = await opening.ConfigureAwait(false);
            await CloseContext(context).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // Observe an abandoned browser operation. The caller still owns the browser process.
        }
        catch (TimeoutException)
        {
            // The browser can no longer acknowledge the context cleanup.
        }
    }

    private static Task CloseContext(IBrowserContext context) => context.CloseAsync().WaitAsync(CleanupTimeout);

    private static void RequireOffline(int attemptedRequest)
    {
        if (attemptedRequest != 0)
        {
            throw new InvalidDataException("The document requested an external asset.");
        }
    }
}
