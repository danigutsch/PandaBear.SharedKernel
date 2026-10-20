using Microsoft.Extensions.Time.Testing;

namespace SharedKernel.RepoConfig.Tests;

internal sealed class TimingOutHttpMessageHandler(
    FakeTimeProvider timeProvider,
    TimeSpan? advance = null) : HttpMessageHandler
{
    private readonly TimeSpan _advance = advance ?? TimeSpan.FromSeconds(30);
    private readonly FakeTimeProvider _timeProvider = timeProvider;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _timeProvider.Advance(_advance);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage());
    }
}
