// Adapted from ViajantesTurismo. See NOTICE.ViajantesTurismo.txt for the MIT license and attribution.

using System.Net;
using System.Net.Mime;

namespace PandaBear.HttpClients;

/// <summary>Transfers an upstream streaming response to a caller-owned result.</summary>
public static class HttpResponseBody
{
    /// <summary>
    /// Reads an upstream success response as a stream. The factory takes ownership of both the response and its stream.
    /// On a missing response or a failure, this method disposes the response.
    /// </summary>
    /// <typeparam name="TBody">The caller's response and stream owner.</typeparam>
    /// <param name="response">The response read with <see cref="HttpCompletionOption.ResponseHeadersRead"/>.</param>
    /// <param name="takeOwnership">Builds an owner from the response, body, and its media type. A missing media type is binary.</param>
    /// <param name="ct">The token observed while opening the stream.</param>
    /// <returns>The owner for a success response, or null for HTTP 404.</returns>
    /// <exception cref="ArgumentNullException">The response or ownership factory is null.</exception>
    /// <exception cref="HttpRequestException">An upstream status other than success or HTTP 404 was returned.</exception>
    /// <exception cref="InvalidOperationException">The factory returned null.</exception>
    /// <exception cref="OperationCanceledException">The stream open was canceled.</exception>
    public static async ValueTask<TBody?> ReadOrNotFound<TBody>(
        HttpResponseMessage response, Func<HttpResponseMessage, Stream, string, TBody> takeOwnership, CancellationToken ct)
        where TBody : class
    {
        ArgumentNullException.ThrowIfNull(response);
        var transferred = false;
        try
        {
            ArgumentNullException.ThrowIfNull(takeOwnership);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? MediaTypeNames.Application.Octet;
            var owner = takeOwnership(response, stream, mediaType)
                ?? throw new InvalidOperationException("The response ownership factory returned null.");
            transferred = true;
            return owner;
        }
        finally
        {
            if (!transferred)
            {
                response.Dispose();
            }
        }
    }
}
