using System.Net;
using System.Text;

namespace PandaBear.Testing.Http;

/// <summary>
/// Creates deterministic HTTP responses for test doubles.
/// </summary>
public static class HttpResponseFactory
{
    /// <summary>
    /// Creates an HTTP JSON response with UTF-8 content.
    /// </summary>
    /// <param name="json">The JSON body.</param>
    /// <param name="statusCode">The status code.</param>
    /// <returns>The HTTP response message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <see langword="null"/>.</exception>
    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(json);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Invokes a test response delegate and rejects a missing response.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the delegate returns <see langword="null"/>.</exception>
    internal static HttpResponseMessage Create(Func<CapturedHttpRequest, HttpResponseMessage> respond, CapturedHttpRequest request) =>
        respond(request) ?? throw new InvalidOperationException(
            $"The response delegate returned no response for {request.Method} {request.PathAndQuery}.");
}
