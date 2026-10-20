using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.RepoConfig.Tests;

internal sealed class TimeAdvancingHttpMessageHandler(FakeTimeProvider timeProvider) : HttpMessageHandler
{
    private readonly FakeTimeProvider _timeProvider = timeProvider;
    private int _requestCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requestCount++;

        return Task.FromResult(CreateResponse());
    }

    private HttpResponseMessage CreateResponse()
    {
        return _requestCount switch
        {
            1 or 4 => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            },
            3 => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new TimeAdvancingHttpContent(_timeProvider)
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            }
        };
    }

    private sealed class TimeAdvancingHttpContent(FakeTimeProvider timeProvider) : HttpContent
    {
        private readonly FakeTimeProvider _timeProvider = timeProvider;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timeProvider.Advance(TimeSpan.FromSeconds(30));
            }

            base.Dispose(disposing);
        }
    }
}
