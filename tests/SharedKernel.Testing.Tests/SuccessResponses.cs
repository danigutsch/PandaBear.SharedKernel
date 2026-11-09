using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PandaBear.Testing.Tests;

/// <summary>Builds the responses that the success assertion tests assert on.</summary>
internal static class SuccessResponses
{
    /// <summary>The cancellation token of the running test.</summary>
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Creates a response with a text body.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="body">The body text.</param>
    /// <param name="mediaType">The media type of the body.</param>
    /// <returns>The response, which the caller disposes.</returns>
    public static HttpResponseMessage Text(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        };

    /// <summary>Creates a response with a binary body and a content type.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="body">The body bytes.</param>
    /// <param name="mediaType">The media type, or null for a body without a <c>Content-Type</c>.</param>
    /// <returns>The response, which the caller disposes.</returns>
    public static HttpResponseMessage Bytes(HttpStatusCode status, byte[] body, string? mediaType)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
        response.Content.Headers.ContentType = mediaType is null ? null : MediaTypeHeaderValue.Parse(mediaType);
        return response;
    }

    /// <summary>Creates a response with an empty body.</summary>
    /// <param name="status">The status code.</param>
    /// <returns>The response, which the caller disposes.</returns>
    public static HttpResponseMessage Empty(HttpStatusCode status) => new(status);

    /// <summary>Creates a 200 file response with a media type and a <c>Content-Disposition</c> header text.</summary>
    /// <param name="mediaType">The media type, with any parameters.</param>
    /// <param name="disposition">The <c>Content-Disposition</c> header text, or null to omit it.</param>
    /// <param name="body">The file bytes.</param>
    /// <returns>The response, which the caller disposes.</returns>
    public static HttpResponseMessage File(string mediaType, string? disposition, params byte[] body)
    {
        var response = Bytes(HttpStatusCode.OK, body, mediaType);
        response.Content.Headers.ContentDisposition = disposition is null ? null : ContentDispositionHeaderValue.Parse(disposition);
        return response;
    }

    /// <summary>Creates a response with the headers that <c>HttpCacheHeaders.SetNoStore</c> writes.</summary>
    /// <returns>The response, which the caller disposes.</returns>
    public static HttpResponseMessage NoStore()
    {
        var response = Empty(HttpStatusCode.OK);
        response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
        response.Headers.Pragma.Add(new NameValueHeaderValue("no-cache"));
        response.Content.Headers.Expires = DateTimeOffset.UnixEpoch;
        return response;
    }
}
