using System.Net;
using System.Text;

namespace SharedKernel.Testing.Contracts;

/// <summary>
/// Provides deterministic HTTP responses for contract client seam tests.
/// </summary>
public sealed class ContractHttpClientTestHelper : HttpClient
{
    private ContractHttpClientTestHelper(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : base(new ResponseHandler(responseFactory), disposeHandler: true)
    {
        BaseAddress = new UriBuilder(Uri.UriSchemeHttps, "contracts.example").Uri;
    }

    /// <summary>
    /// Creates an HTTP client backed by a deterministic response factory.
    /// </summary>
    /// <param name="responseFactory">Creates the response for each request.</param>
    /// <returns>An HTTP client with a stable base address.</returns>
    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        ArgumentNullException.ThrowIfNull(responseFactory);

        return new ContractHttpClientTestHelper(responseFactory);
    }

    /// <summary>
    /// Creates a JSON response with the supplied status code.
    /// </summary>
    /// <param name="json">The JSON response body.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns>A JSON HTTP response.</returns>
    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(json);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
