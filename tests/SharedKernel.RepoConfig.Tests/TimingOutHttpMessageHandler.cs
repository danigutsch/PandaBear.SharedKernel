using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.RepoConfig.Tests;

internal sealed class TimingOutHttpMessageHandler(FakeTimeProvider timeProvider) : HttpMessageHandler
{
    private readonly FakeTimeProvider _timeProvider = timeProvider;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage());
    }
}
