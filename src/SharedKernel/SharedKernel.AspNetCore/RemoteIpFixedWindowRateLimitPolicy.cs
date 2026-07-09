namespace SharedKernel.AspNetCore;

/// <summary>
/// Defines an application-owned fixed-window rate-limit policy partitioned by remote IP address.
/// </summary>
/// <param name="Name">The application-owned policy name.</param>
/// <param name="PermitLimit">The maximum number of permitted requests per window.</param>
/// <param name="Window">The fixed window duration.</param>
public sealed record RemoteIpFixedWindowRateLimitPolicy(string Name, int PermitLimit, TimeSpan Window);
