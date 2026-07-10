using System.Net;
using System.Text;

namespace SharedKernel.Branding.Tests;

internal sealed class BrandingApiClientTestHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public HttpMethod? LastMethod { get; private set; }

    public string? LastPath { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastPath = request.RequestUri?.PathAndQuery;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
    }
}
