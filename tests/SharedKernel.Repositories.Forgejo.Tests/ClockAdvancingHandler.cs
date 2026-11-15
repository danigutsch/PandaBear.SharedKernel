using Microsoft.Extensions.Time.Testing;

namespace PandaBear.Repositories.Forgejo.Tests;

/// <summary>Advances a fake clock past the request timeout before it answers, so the request times out.</summary>
/// <param name="timeProvider">The fake clock to advance.</param>
internal sealed class ClockAdvancingHandler(FakeTimeProvider timeProvider) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        timeProvider.Advance(TimeSpan.FromSeconds(31));
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new HttpResponseMessage());
    }
}
