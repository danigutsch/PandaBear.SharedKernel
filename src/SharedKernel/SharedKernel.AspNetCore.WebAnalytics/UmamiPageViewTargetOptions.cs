using Microsoft.AspNetCore.HttpOverrides;

namespace SharedKernel.AspNetCore.WebAnalytics;

/// <summary>
/// Settings of the Umami target that <see cref="UmamiPageViewBuilderExtensions.AddUmami"/> adds, named after its connection name and
/// checked when the host starts. Umami's address and the website id come from the connection string.
/// </summary>
public sealed class UmamiPageViewTargetOptions
{
    /// <summary>
    /// Gets or sets the request header that carries the visitor's shortened address (<see cref="PageView.ClientIp"/>) to Umami,
    /// <c>X-Forwarded-For</c> by default. Umami must read the address from it: the Umami Aspire resource sets its <c>CLIENT_IP_HEADER</c>
    /// to the default.
    /// </summary>
    public string ClientIpHeader { get; set; } = ForwardedHeadersDefaults.XForwardedForHeaderName;

    /// <summary>Gets or sets the connection string read from the configuration, which <see cref="UmamiConnection"/> parses.</summary>
    internal string? ConnectionString { get; set; }
}
