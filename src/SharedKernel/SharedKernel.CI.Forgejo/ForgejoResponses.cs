using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PandaBear.IO;

namespace PandaBear.CI.Forgejo;

/// <summary>Sends the GET requests of the Forgejo provider with a timeout and reads their bounded JSON bodies.</summary>
internal static class ForgejoResponses
{
    /// <summary>How long one request may take.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(1);

    private const int MaxBodyBytes = 16 * 1024 * 1024;

    /// <summary>Gets <paramref name="uri"/> and reads its body as <typeparamref name="T"/>, with the <c>Link</c> header of the response.</summary>
    /// <exception cref="HttpRequestException">The server answered with another status than 200 OK.</exception>
    /// <exception cref="InvalidDataException">The body is larger than 16 MiB or not valid.</exception>
    /// <exception cref="TimeoutException">The request took longer than a minute.</exception>
    public static async Task<(T Value, string? Link)> Get<T>(HttpClient client, Uri uri, JsonTypeInfo<T> typeInfo, string what, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException(Refused("GET", uri, response.StatusCode), null, response.StatusCode);
            }

            var body = await ReadBody(response.Content, uri, timeout.Token).ConfigureAwait(false);
            T? value;
            try
            {
                value = JsonSerializer.Deserialize(body, typeInfo);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"GET {uri} returned {what} that is not valid: {exception.Message}", exception);
            }

            if (value is null)
            {
                throw new InvalidDataException($"GET {uri} returned {what} that is not valid: it is null.");
            }

            var link = response.Headers.TryGetValues("Link", out var links) ? string.Join(',', links) : null;
            return (value, link);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"GET {uri} did not finish within {RequestTimeout.TotalSeconds} seconds."), exception);
        }
    }

    /// <summary>Returns the message of a refused request.</summary>
    public static string Refused(string method, Uri uri, HttpStatusCode status) =>
        string.Create(CultureInfo.InvariantCulture, $"{method} {uri} returned {(int)status} {status}.");

    private static async Task<byte[]> ReadBody(HttpContent content, Uri uri, CancellationToken ct)
    {
        if (content.Headers.ContentLength > MaxBodyBytes)
        {
            throw new InvalidDataException($"GET {uri} returned more than {MaxBodyBytes} bytes.");
        }

        var source = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            return await BoundedReader.ReadToEnd(source, MaxBodyBytes, ct).ConfigureAwait(false)
                ?? throw new InvalidDataException($"GET {uri} returned more than {MaxBodyBytes} bytes.");
        }
    }
}
