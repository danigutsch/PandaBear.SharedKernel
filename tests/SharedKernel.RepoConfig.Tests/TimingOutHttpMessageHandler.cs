using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// HTTP message handler that advances the fake clock before each response so that deadlines expire.
/// </summary>
/// <param name="timeProvider">The fake clock to advance.</param>
/// <param name="advance">The amount to advance per request; 30 seconds when null.</param>
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
