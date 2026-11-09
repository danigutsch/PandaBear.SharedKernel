using System.Net;
using System.Net.Http.Headers;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// Sends one HTTPS request with a time limit and a limit on the response size. A redirect, another host, a plain HTTP
/// address, or an oversized body is an error. Messages never carry the response body or the request address.
/// </summary>
internal static class BoundedHttp
{
    private const int BufferSize = 16 * 1024;

    /// <summary>Sends <paramref name="request"/> and returns the body of a successful response.</summary>
    /// <param name="client">The client; its handler is the only network seam.</param>
    /// <param name="request">The request; its address must be HTTPS on <paramref name="host"/>.</param>
    /// <param name="host">The only host that may answer.</param>
    /// <param name="maxBytes">The longest body that is accepted.</param>
    /// <param name="timeout">The time allowed for the whole exchange.</param>
    /// <param name="what">What is being fetched, for messages.</param>
    /// <param name="cancellationToken">Cancels the call; this throws, a timeout does not.</param>
    public static async Task<Result<byte[]>> Send(
        HttpClient client,
        HttpRequestMessage request,
        string host,
        int maxBytes,
        TimeSpan timeout,
        string what,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { Scheme: "https" } uri || !string.Equals(uri.Host, host, StringComparison.Ordinal))
        {
            return Result.Invalid<byte[]>($"{what}: the address must be HTTPS on {host}.", "address", "The address must be HTTPS on the expected host.");
        }

        request.Headers.UserAgent.ParseAdd(PackageHttp.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
            return await Read(response, host, maxBytes, what, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Unavailable<byte[]>($"{what}: no answer within {timeout.TotalSeconds:0} seconds.");
        }
        catch (HttpRequestException exception)
        {
            return Result.Unavailable<byte[]>($"{what}: the request failed ({exception.HttpRequestError}).");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            // A connection reset in the middle of the body, or a truncated or corrupt compressed body.
            return Result.Unavailable<byte[]>($"{what}: the response body could not be read completely.");
        }
    }

    private static async Task<Result<byte[]>> Read(HttpResponseMessage response, string host, int maxBytes, string what, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status is >= 300 and < 400)
        {
            return Result.Unavailable<byte[]>($"{what}: the service answered with a redirect, which is refused.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.NotFound<byte[]>($"{what}: not found.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return Result.Unavailable<byte[]>($"{what}: the service answered HTTP {status}.");
        }

        if (response.RequestMessage?.RequestUri is { } final && (final.Scheme != "https" || !string.Equals(final.Host, host, StringComparison.Ordinal)))
        {
            return Result.Unavailable<byte[]>($"{what}: the answer came from another host or over plain HTTP, which is refused.");
        }

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            return Result.Unavailable<byte[]>($"{what}: the response is larger than {maxBytes} bytes.");
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var body = new MemoryStream();
            var buffer = new byte[BufferSize];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (body.Length + read > maxBytes)
                {
                    return Result.Unavailable<byte[]>($"{what}: the response is larger than {maxBytes} bytes.");
                }

                await body.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return Result.Ok(body.ToArray());
        }
    }
}
