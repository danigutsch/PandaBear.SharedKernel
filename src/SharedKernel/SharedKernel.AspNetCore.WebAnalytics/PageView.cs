using System.Net;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// One page view as every target receives it: what the server keeps of a page it answered. It carries the visitor's address and
/// <c>User-Agent</c>, which a target may need (Umami derives the country, the device and a salted visitor hash from them), so never log
/// it; it has no <c>ToString</c> that would print them.
/// </summary>
public sealed class PageView
{
    /// <summary>
    /// Gets the host name of the page: <see cref="PageViewOptions.Hostname"/> when it is set, otherwise the request host without its port,
    /// or <see langword="null"/> when the request had none.
    /// </summary>
    public string? Hostname { get; init; }

    /// <summary>Gets the escaped path of the page with only its <c>utm_*</c> query values, such as <c>/pt/viagens?utm_source=newsletter</c>.</summary>
    public required string PathAndQuery { get; init; }

    /// <summary>Gets the address of the referring page on another site, without query, fragment or user info, or <see langword="null"/>.</summary>
    public string? Referrer { get; init; }

    /// <summary>Gets the first language tag the browser asked for, such as <c>pt-BR</c>, or <see langword="null"/>.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the visitor's <c>User-Agent</c>, which is never empty.</summary>
    public required string UserAgent { get; init; }

    /// <summary>
    /// Gets the visitor's address, with IPv4-mapped addresses as IPv4 and no IPv6 scope, or <see langword="null"/> when the connection had
    /// none.
    /// </summary>
    public IPAddress? ClientIp { get; init; }
}
