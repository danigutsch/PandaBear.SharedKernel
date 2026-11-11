using System.Net;

namespace PandaBear.HttpClients.Tests;

/// <summary>
/// Tests that a 429 answer can be made final: it is neither retried, which would spend more of the quota that was just exhausted, nor
/// counted by the circuit breaker, which would cut off every other request; other failures keep the standard behavior.
/// </summary>
public sealed class TooManyRequestsResilienceTests
{
    [Fact]
    public async Task A_429_is_retried_by_the_standard_pipeline()
    {
        // Act
        var (statuses, attempts) = await TooManyRequestsTestServices.Send(_ => { }, "/content", requests: 1, failures: 1, HttpStatusCode.TooManyRequests, TestContext.Current.CancellationToken);

        // Assert
        statuses.ShouldBe([HttpStatusCode.OK]);
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task A_429_is_answered_at_once_and_never_opens_the_circuit()
    {
        // Act
        var (statuses, attempts) = await TooManyRequestsTestServices.Send(
            options => options.DisableRetryOnTooManyRequests(),
            "/content",
            requests: 102,
            failures: 101,
            HttpStatusCode.TooManyRequests,
            TestContext.Current.CancellationToken);

        // Assert
        statuses.Take(101).ToArray().ShouldBe(Enumerable.Repeat(HttpStatusCode.TooManyRequests, 101).ToArray());
        statuses[101].ShouldBe(HttpStatusCode.OK);
        attempts.ShouldBe(102);
    }

    [Fact]
    public async Task A_scoped_rule_leaves_other_requests_with_the_standard_retry()
    {
        // Act
        var scoped = await TooManyRequestsTestServices.Send(
            options => options.DisableRetryOnTooManyRequests(TooManyRequestsTestServices.IsQuotaRoute),
            "/visitor-quota/content",
            requests: 1,
            failures: 1,
            HttpStatusCode.TooManyRequests,
            TestContext.Current.CancellationToken);
        var other = await TooManyRequestsTestServices.Send(
            options => options.DisableRetryOnTooManyRequests(TooManyRequestsTestServices.IsQuotaRoute),
            "/content",
            requests: 1,
            failures: 1,
            HttpStatusCode.TooManyRequests,
            TestContext.Current.CancellationToken);

        // Assert
        scoped.Statuses.ShouldBe([HttpStatusCode.TooManyRequests]);
        scoped.Attempts.ShouldBe(1);
        other.Statuses.ShouldBe([HttpStatusCode.OK]);
        other.Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Other_transient_failures_are_still_retried()
    {
        // Act
        var (statuses, attempts) = await TooManyRequestsTestServices.Send(
            options => options.DisableRetryOnTooManyRequests(),
            "/content",
            requests: 1,
            failures: 1,
            HttpStatusCode.ServiceUnavailable,
            TestContext.Current.CancellationToken);

        // Assert
        statuses.ShouldBe([HttpStatusCode.OK]);
        attempts.ShouldBe(2);
    }

    [Fact]
    public void Null_options_are_refused()
    {
        // Act
        Action act = () => HttpResilienceOptionsExtensions.DisableRetryOnTooManyRequests(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
