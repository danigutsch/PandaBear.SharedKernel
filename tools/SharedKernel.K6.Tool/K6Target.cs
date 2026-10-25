using System.Net;

namespace PandaBear.K6.Tool;

/// <summary>Validates the base URL that k6 targets and rewrites it for k6 in a container.</summary>
internal static class K6Target
{
    /// <summary>The host name through which a container reaches the machine that runs it.</summary>
    public const string ContainerHostAlias = "host.docker.internal";

    /// <summary>Parses and validates a base URL.</summary>
    /// <param name="value">The URL text; surrounding white space is ignored.</param>
    /// <param name="source">Names where the URL came from in errors.</param>
    /// <param name="allowExternal">Whether a host other than a loopback host is allowed.</param>
    /// <exception cref="InvalidOperationException">The URL is not an allowed target.</exception>
    public static Uri Parse(string value, string source, bool allowExternal)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var target)
            || (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{source} must be an absolute http:// or https:// URL.");
        }

        if (target.UserInfo.Length > 0)
        {
            throw new InvalidOperationException($"{source} must not include user information.");
        }

        if (target.Query.Length > 0 || target.Fragment.Length > 0)
        {
            throw new InvalidOperationException($"{source} must not include a query or fragment.");
        }

        if (!allowExternal && !target.IsLoopback)
        {
            throw new InvalidOperationException($"{source} must target a loopback host such as localhost, 127.0.0.1, or [::1] unless --allow-external-target is passed.");
        }

        return target;
    }

    /// <summary>Formats a base URL without a trailing <c>/</c>.</summary>
    public static string Format(Uri target) => target.AbsoluteUri.TrimEnd('/');

    /// <summary>Formats a base URL for k6 in a container, which reaches loopback and unspecified hosts through the host alias.</summary>
    public static string ForContainer(Uri target) =>
        target.IsLoopback || IsUnspecified(target)
            ? Format(new UriBuilder(target) { Host = ContainerHostAlias }.Uri)
            : Format(target);

    /// <summary>Replaces the unspecified address that a host bound to, such as <c>0.0.0.0</c>, with <c>127.0.0.1</c>.</summary>
    /// <returns>The URL with the loopback address, or <paramref name="value"/> when it is not an absolute URL.</returns>
    public static string ToLoopback(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var target))
        {
            return value;
        }

        return IsUnspecified(target)
            ? new UriBuilder(target) { Host = IPAddress.Loopback.ToString() }.Uri.AbsoluteUri
            : target.AbsoluteUri;
    }

    private static bool IsUnspecified(Uri target) =>
        target.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
        && IPAddress.TryParse(target.DnsSafeHost, out var address)
        && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
}
