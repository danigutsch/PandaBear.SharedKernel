using System.Net;

namespace PandaBear.HttpClients.Tests;

[Trait(Testing.TestTraitNames.CategoryName, Testing.TestTraitValues.SecurityCategory)]
public sealed class HttpClientResilienceCustomizationTests
{
    [Fact]
    public async Task Request_scoped_quota_policy_preserves_other_responses_and_does_not_open_the_circuit()
    {
        // Act
        var observation = await HttpClientDefaultsTestServices.SendPathScopedQuotaResponses(TestContext.Current.CancellationToken);

        // Assert
        observation.ObservedPaths.ShouldContain("/visitor-quota/content");
        observation.ObservedPaths.ShouldContain("/content");
        observation.QuotaStatuses.Length.ShouldBe(102);
        var quotaResponses = observation.QuotaStatuses.Take(101).ToArray();
        quotaResponses.ShouldBe(Enumerable.Repeat(HttpStatusCode.TooManyRequests, 101).ToArray());
        observation.QuotaStatuses[101].ShouldBe(HttpStatusCode.OK);
        observation.QuotaAttempts.ShouldBe(102);
        observation.OrdinaryStatus.ShouldBe(HttpStatusCode.OK);
        observation.OrdinaryAttempts.ShouldBe(2);
        observation.TransientStatus.ShouldBe(HttpStatusCode.OK);
        observation.TransientAttempts.ShouldBe(2);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("CONNECT")]
    public async Task Customization_cannot_reenable_retries_for_unsafe_requests(string method)
    {
        // Act
        var observation = await HttpClientDefaultsTestServices.SendTransientAfterCommit(
            new HttpMethod(method),
            includeStreamingContent: false,
            options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(true),
            TestContext.Current.CancellationToken);

        // Assert
        observation.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        observation.AttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task Customization_cannot_replay_a_streamed_payment_even_when_its_predicate_accepts_retry()
    {
        // Act
        var observation = await HttpClientDefaultsTestServices.SendTransientAfterCommit(
            HttpMethod.Post,
            includeStreamingContent: true,
            options => options.Retry.ShouldHandle = _ => ValueTask.FromResult(true),
            TestContext.Current.CancellationToken);

        // Assert
        observation.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        observation.AttemptCount.ShouldBe(1);
        observation.ContentSerializationCount.ShouldBe(1);
        observation.SourceBytesRead.ShouldBe(HttpClientDefaultsTestServices.StreamingPayloadLength);
    }

    [Fact]
    public void Customization_rejects_a_null_service_collection()
    {
        // Arrange
        Action register = () => HttpServiceCollectionExtensions.AddHttpClientDefaults(null!, configureResilience: null);

        // Act
        var exception = register.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("services");
    }
}
