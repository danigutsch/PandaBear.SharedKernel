using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

internal sealed class TimeAdvancingHttpMessageHandler(FakeTimeProvider timeProvider) : HttpMessageHandler
{
    private readonly FakeTimeProvider _timeProvider = timeProvider;
    private int _requestCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requestCount++;

        return Task.FromResult(CreateResponse(request));
    }

    private HttpResponseMessage CreateResponse(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("/issues/", StringComparison.Ordinal))
        {
            var number = int.Parse(request.RequestUri.Segments[^1], System.Globalization.CultureInfo.InvariantCulture);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(GitHubSyncTestResponseFactory.IssueMetadata(number)) };
        }

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
