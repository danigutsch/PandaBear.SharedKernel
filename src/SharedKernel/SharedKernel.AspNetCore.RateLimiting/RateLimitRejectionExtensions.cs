using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace PandaBear.AspNetCore.RateLimiting;

/// <summary>Shapes the response of a request the rate limiter rejected.</summary>
public static class RateLimitRejectionExtensions
{
    private const string ExpiredAtUnixEpochHttpDate = "Thu, 01 Jan 1970 00:00:00 GMT";

    /// <summary>
    /// Answers a rejected request with 429 Too Many Requests that browsers and proxies never store (<c>Cache-Control: no-store</c>,
    /// <c>Pragma: no-cache</c> and an expired <c>Expires</c>), so a cached rejection cannot outlive the limit, and with <c>Retry-After</c> in
    /// whole seconds, rounded up, when the limiter reports when a permit is available. A callback already set on
    /// <see cref="RateLimiterOptions.OnRejected"/> runs afterwards, so it can still change the response.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <returns>The same options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static RateLimiterOptions UseNoStoreRetryAfterRejection(this RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        var inner = options.OnRejected;
        options.OnRejected = (context, cancellationToken) =>
        {
            var headers = context.HttpContext.Response.Headers;
            headers.CacheControl = "no-store";
            headers[HeaderNames.Pragma] = "no-cache";
            headers[HeaderNames.Expires] = ExpiredAtUnixEpochHttpDate;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                headers.RetryAfter = Math.Ceiling(Math.Max(retryAfter.TotalSeconds, 0)).ToString(CultureInfo.InvariantCulture);
            }

            return inner is null ? ValueTask.CompletedTask : inner(context, cancellationToken);
        };

        return options;
    }
}
