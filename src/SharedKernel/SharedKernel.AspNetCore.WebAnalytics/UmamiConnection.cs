using System.Data.Common;

namespace PandaBear.AspNetCore.WebAnalytics;

/// <summary>
/// Umami's address and the website id, parsed from a connection string <c>Endpoint=&lt;address&gt;;WebsiteId=&lt;id&gt;</c>, the form the
/// website resource of <c>PandaBear.SharedKernel.Aspire.Hosting.Umami</c> hands to a site. Keys are case-insensitive; other keys are ignored.
/// </summary>
internal sealed class UmamiConnection
{
    /// <summary>The key of Umami's base address.</summary>
    public const string EndpointKey = "Endpoint";

    /// <summary>The key of the website id.</summary>
    public const string WebsiteIdKey = "WebsiteId";

    private const string SendPath = "api/send";

    private UmamiConnection(Uri sendAddress, Guid websiteId)
    {
        SendAddress = sendAddress;
        WebsiteId = websiteId;
    }

    /// <summary>Gets the address page views are posted to: <c>api/send</c> below the endpoint, keeping the endpoint's path.</summary>
    public Uri SendAddress { get; }

    /// <summary>Gets the website id.</summary>
    public Guid WebsiteId { get; }

    /// <summary>
    /// Parses a connection string whose endpoint is an absolute http or https address without user info, query or fragment and whose
    /// website id is a GUID other than the empty one.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The connection, or <see langword="null"/> when the connection string is not of that form.</returns>
    public static UmamiConnection? Parse(string? connectionString)
    {
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (!builder.TryGetValue(EndpointKey, out var endpointValue)
            || !Uri.TryCreate(endpointValue as string, UriKind.Absolute, out var endpoint)
            || !IsBaseAddress(endpoint)
            || !builder.TryGetValue(WebsiteIdKey, out var websiteIdValue)
            || !Guid.TryParse(websiteIdValue as string, out var websiteId)
            || websiteId == Guid.Empty)
        {
            return null;
        }

        return new UmamiConnection(SendAddressBelow(endpoint), websiteId);
    }

    private static bool IsBaseAddress(Uri endpoint) =>
        (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps)
        && endpoint.UserInfo.Length == 0
        && endpoint.Query.Length == 0
        && endpoint.Fragment.Length == 0;

    private static Uri SendAddressBelow(Uri endpoint)
    {
        var path = endpoint.AbsolutePath.EndsWith('/') ? endpoint.AbsoluteUri : endpoint.AbsoluteUri + "/";
        return new Uri(new Uri(path, UriKind.Absolute), SendPath);
    }
}
