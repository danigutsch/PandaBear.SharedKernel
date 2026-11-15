using System.Net;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// What the forwarder keeps of one page view until it is sent. It holds the visitor's address and browser for Umami alone, so it is
/// never logged; it has no <c>ToString</c> that would print them.
/// </summary>
internal sealed class PageView
{
    /// <summary>Gets the Umami website id.</summary>
    public required Guid WebsiteId { get; init; }

    /// <summary>Gets the host name of the page, or <see langword="null"/> when the request had none.</summary>
    public string? Hostname { get; init; }

    /// <summary>Gets the escaped path of the page with only its <c>utm_*</c> query values.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the address of the referring page on another site, without query and fragment, or <see langword="null"/>.</summary>
    public string? Referrer { get; init; }

    /// <summary>Gets the first language tag the browser asked for, or <see langword="null"/>.</summary>
    public string? Language { get; init; }

    /// <summary>Gets the visitor's <c>User-Agent</c>, which Umami needs to register the page view and to recognise bots.</summary>
    public required string UserAgent { get; init; }

    /// <summary>Gets the visitor's address, or <see langword="null"/> when the connection had none.</summary>
    public IPAddress? ClientIp { get; init; }
}
