using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace SharedKernel.AspNetCore.RateLimiting.Tests;

/// <summary>
/// Tests the opt-in rejection response: 429, never stored by a browser or proxy, with <c>Retry-After</c> in whole seconds when the limiter
/// knows it, and the host's own callback kept.
/// </summary>
public sealed class RateLimitRejectionResponseTests
{
    [Theory]
    [InlineData(2.3, "3")]
    [InlineData(60, "60")]
    [InlineData(0, "0")]
    public async Task A_rejection_is_not_stored_and_says_when_to_retry_in_whole_seconds(double seconds, string expected)
    {
        // Arrange
        var options = new RateLimiterOptions().UseNoStoreRetryAfterRejection();
        var context = new DefaultHttpContext();

        // Act
        await options.OnRejected.ShouldNotBeNull()(
            new OnRejectedContext { HttpContext = context, Lease = new RetryAfterRateLimitLease(TimeSpan.FromSeconds(seconds)) },
            TestContext.Current.CancellationToken);

        // Assert
        options.RejectionStatusCode.ShouldBe(StatusCodes.Status429TooManyRequests);
        context.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        context.Response.Headers.Pragma.ToString().ShouldBe("no-cache");
        context.Response.Headers.RetryAfter.ToString().ShouldBe(expected);
    }

    [Fact]
    public async Task A_rejection_without_a_known_wait_has_no_retry_after()
    {
        // Arrange
        var options = new RateLimiterOptions().UseNoStoreRetryAfterRejection();
        var context = new DefaultHttpContext();

        // Act
        await options.OnRejected.ShouldNotBeNull()(
            new OnRejectedContext { HttpContext = context, Lease = new RejectedRateLimitLease() },
            TestContext.Current.CancellationToken);

        // Assert
        context.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        context.Response.Headers.ContainsKey("Retry-After").ShouldBeFalse();
    }

    [Fact]
    public async Task The_existing_callback_still_runs_after_the_headers_are_set()
    {
        // Arrange
        string? cacheControlSeenByCallback = null;
        var options = new RateLimiterOptions
        {
            OnRejected = (rejected, _) =>
            {
                cacheControlSeenByCallback = rejected.HttpContext.Response.Headers.CacheControl.ToString();
                return ValueTask.CompletedTask;
            }
        };
        options.UseNoStoreRetryAfterRejection();
        var context = new DefaultHttpContext();

        // Act
        await options.OnRejected.ShouldNotBeNull()(
            new OnRejectedContext { HttpContext = context, Lease = new RejectedRateLimitLease() },
            TestContext.Current.CancellationToken);

        // Assert
        cacheControlSeenByCallback.ShouldBe("no-store");
    }

    [Fact]
    public void Null_options_are_refused()
    {
        // Act
        Action act = () => RateLimitRejectionExtensions.UseNoStoreRetryAfterRejection(null!);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }
}
