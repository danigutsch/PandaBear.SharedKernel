using System.Data.Common;
using System.Text;

namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Credentials and address split out of a database URI, URL with user info, or connection string.</summary>
/// <param name="Username">User name, when present.</param>
/// <param name="Password">Password, when present.</param>
/// <param name="Host">Host name, when present.</param>
/// <param name="Uri">Address without credentials or query, suitable for <c>login.uris</c>.</param>
internal sealed record ConnectionParts(string? Username, string? Password, string? Host, string? Uri)
{
    private static readonly string[] PasswordKeys = ["password", "pwd"];
    private static readonly string[] UserKeys = ["user id", "uid", "user", "username", "user name"];
    private static readonly string[] HostKeys = ["server", "data source", "host", "address", "addr", "network address", "hostname"];

    /// <summary>Splits <c>scheme://user:password@host[:port][/path][?query]</c>.</summary>
    /// <param name="value">URI text.</param>
    /// <param name="parts">Split parts.</param>
    /// <returns><see langword="true"/> when the URI has user info with a password.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool TryParseUri(string value, out ConnectionParts? parts)
    {
        ArgumentNullException.ThrowIfNull(value);
        parts = null;
        int schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            return false;
        }

        int authorityStart = schemeEnd + 3;
        int at = value.IndexOf('@', authorityStart);
        int slash = value.IndexOfAny(['/', '?', '#'], authorityStart);
        if (at < 0 || (slash >= 0 && slash < at))
        {
            return false;
        }

        string userInfo = value[authorityStart..at];
        int colon = userInfo.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return false;
        }

        int pathStart = value.IndexOfAny(['/', '?', '#'], at);
        string host = pathStart < 0 ? value[(at + 1)..] : value[(at + 1)..pathStart];
        int query = value.IndexOfAny(['?', '#'], at);
        string address = value[..authorityStart] + (query < 0 ? value[(at + 1)..] : value[(at + 1)..query]);
        string hostName = host.StartsWith('[') ? host : host.Split(':')[0];
        try
        {
            parts = new ConnectionParts(
                NullIfEmpty(System.Uri.UnescapeDataString(userInfo[..colon])),
                NullIfEmpty(System.Uri.UnescapeDataString(userInfo[(colon + 1)..])),
                NullIfEmpty(hostName),
                address);
        }
        catch (UriFormatException)
        {
            return false;
        }

        return parts.Password is not null;
    }

    /// <summary>Splits an ADO.NET or libpq keyword connection string.</summary>
    /// <param name="value">Connection string.</param>
    /// <param name="parts">Split parts.</param>
    /// <returns><see langword="true"/> when the string parses and has a password.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static bool TryParseConnectionString(string value, out ConnectionParts? parts)
    {
        ArgumentNullException.ThrowIfNull(value);
        parts = null;
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = value.Trim();
        }
        catch (ArgumentException)
        {
            return false;
        }

        string? password = Find(builder, PasswordKeys);
        string? host = Find(builder, HostKeys);
        parts = new ConnectionParts(Find(builder, UserKeys), password, host, host);
        return password is not null;
    }

    private static string? Find(DbConnectionStringBuilder builder, string[] keys)
    {
        foreach (string key in keys)
        {
            if (builder.TryGetValue(key, out object? value) && value is string text && text.Length > 0)
            {
                return text;
            }
        }

        return null;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Prints only non-secret members, so values never reach logs or messages through <c>ToString</c>.</summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Host = ").Append(Host);
        return true;
    }
}
