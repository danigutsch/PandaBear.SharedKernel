using System.Net;
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
    /// <remarks>
    /// Applications behind reverse proxies must configure and trust forwarded headers before this policy runs.
    /// Otherwise, the remote IP address may be the proxy address rather than the original client address.
    /// </remarks>
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
            RemoteIpPartitionKey(httpContext.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

        return options;
    }

    private static string RemoteIpPartitionKey(IPAddress? remoteIpAddress)
    {
        if (remoteIpAddress is null)
        {
            return "local";
        }

        return remoteIpAddress.IsIPv4MappedToIPv6
            ? remoteIpAddress.MapToIPv4().ToString()
            : remoteIpAddress.ToString();
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
