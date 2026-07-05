using System.Net;
using System.Text;

namespace SharedKernel.Testing.Http;

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
    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(json);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
