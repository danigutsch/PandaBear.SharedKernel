using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace PandaBear.CI.Tool;

/// <summary>
/// Downloads public release assets over HTTPS only. It follows at most five redirects, each to HTTPS, retries 408, 429,
/// and 5xx answers after <c>Retry-After</c> or a short backoff, gives every attempt a timeout, and stops a body that
/// exceeds its limit while the body streams. It never sends credentials.
/// </summary>
internal sealed class ReleaseAssetClient(HttpClient client, TimeProvider timeProvider, CandidateDownloadLimits limits)
{
    private const int MaxRedirects = 5;
    private const int BufferSize = 81_920;

    /// <summary>Copies the body of <paramref name="uri"/> into <paramref name="destination"/>.</summary>
    /// <param name="uri">The HTTPS address of the asset.</param>
    /// <param name="destination">Receives the body.</param>
    /// <param name="maxBytes">The largest accepted body.</param>
    /// <param name="limit">Names the limit in the error for a larger body, such as <c>the per-package limit of 64 MiB</c>.</param>
    /// <param name="ct">Cancels the download.</param>
    /// <returns>The number of bytes copied.</returns>
    /// <exception cref="HttpRequestException">The server refused the request, or a redirect left HTTPS.</exception>
    /// <exception cref="InvalidDataException">The body is larger than <paramref name="maxBytes"/>.</exception>
    /// <exception cref="TimeoutException">An attempt took longer than the request timeout.</exception>
    public async Task<long> Copy(Uri uri, Stream destination, long maxBytes, string limit, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan retryDelay;
            using (var timeout = new CancellationTokenSource(limits.RequestTimeout, timeProvider))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token))
            {
                try
                {
                    using var response = await Send(uri, linked.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return await CopyBody(uri, response.Content, destination, maxBytes, limit, linked.Token).ConfigureAwait(false);
                    }

                    var status = response.StatusCode;
                    if (!IsTransient(status))
                    {
                        throw new HttpRequestException($"GET {uri} returned {(int)status} {status}.", null, status);
                    }

                    if (attempt == limits.MaxAttempts)
                    {
                        throw new HttpRequestException($"GET {uri} returned {(int)status} {status} after {attempt} attempts.", null, status);
                    }

                    retryDelay = RetryDelay(response.Headers.RetryAfter, attempt);
                }
                catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        string.Create(CultureInfo.InvariantCulture, $"GET {uri} did not finish within {limits.RequestTimeout.TotalSeconds} seconds."),
                        exception);
                }
            }

            await Task.Delay(retryDelay, timeProvider, ct).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status is >= 500 and <= 599;

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static async Task<long> CopyBody(Uri uri, HttpContent content, Stream destination, long maxBytes, string limit, CancellationToken ct)
    {
        if (content.Headers.ContentLength > maxBytes)
        {
            throw new InvalidDataException($"GET {uri} exceeds {limit}.");
        }

        var source = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var buffer = new byte[BufferSize];
            long copied = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                copied += read;
                if (copied > maxBytes)
                {
                    throw new InvalidDataException($"GET {uri} exceeds {limit}.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }

            return copied;
        }
    }

    // Messages name only the requested address: a redirect target carries a signed query.
    private async Task<HttpResponseMessage> Send(Uri uri, CancellationToken ct)
    {
        var current = uri;
        for (var redirects = 0; ; redirects++)
        {
            if (current.Scheme != Uri.UriSchemeHttps)
            {
                throw new HttpRequestException($"GET {uri} redirected to an address that is not HTTPS.");
            }

            HttpResponseMessage response;
            using (var request = new HttpRequestMessage(HttpMethod.Get, current))
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }

            if (!IsRedirect(response.StatusCode))
            {
                return response;
            }

            var location = response.Headers.Location;
            response.Dispose();
            if (location is null)
            {
                throw new HttpRequestException($"GET {uri} returned a redirect without a location.");
            }

            if (redirects == MaxRedirects)
            {
                throw new HttpRequestException($"GET {uri} redirected more than {MaxRedirects} times.");
            }

            current = location.IsAbsoluteUri ? location : new Uri(current, location);
        }
    }

    private TimeSpan RetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        var delay = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - timeProvider.GetUtcNow() : TimeSpan.FromSeconds(1 << (attempt - 1)));
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > limits.MaxRetryDelay ? limits.MaxRetryDelay : delay;
    }
}
