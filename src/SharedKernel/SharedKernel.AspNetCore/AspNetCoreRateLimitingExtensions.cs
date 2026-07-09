using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace SharedKernel.AspNetCore;

/// <summary>
/// Provides reusable rate-limiting helpers for ASP.NET Core applications.
/// </summary>
public static class AspNetCoreRateLimitingExtensions
{
    /// <summary>
    /// Adds a fixed-window rate-limit policy partitioned by remote IP address.
    /// </summary>
    /// <param name="options">The rate limiter options to configure.</param>
    /// <param name="policyName">The application-owned policy name.</param>
    /// <param name="permitLimit">The maximum number of permitted requests per window.</param>
    /// <param name="window">The fixed window duration.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> instance.</returns>
    public static RateLimiterOptions AddRemoteIpFixedWindowPolicy(
        this RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(policyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

        return options;
    }

    /// <summary>
    /// Adds fixed-window rate-limit policies partitioned by remote IP address.
    /// </summary>
    /// <param name="options">The rate limiter options to configure.</param>
    /// <param name="policies">The application-owned rate-limit policy definitions.</param>
    /// <returns>The same <see cref="RateLimiterOptions"/> instance.</returns>
    public static RateLimiterOptions AddRemoteIpFixedWindowPolicies(
        this RateLimiterOptions options,
        IReadOnlyCollection<RemoteIpFixedWindowRateLimitPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(policies);

        foreach (var policy in policies)
        {
            options.AddRemoteIpFixedWindowPolicy(policy.Name, policy.PermitLimit, policy.Window);
        }

        return options;
    }
}
