using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>
/// Comparison keys for item values. Keys are used to compare and group values; they are never written to the output.
/// </summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Hosts, user names, and titles are specified as lowercase keys; they are compared ordinally and never displayed or written.")]
internal static partial class ItemNormalizer
{
    /// <summary>
    /// Lenient host of a stored address: lowercase, without scheme, <c>www.</c>, user info, port, path, query, or
    /// fragment. Bare hosts such as <c>example.com/login</c> and IP addresses are accepted. Addresses with a scheme
    /// other than <c>http</c> or <c>https</c>, such as <c>androidapp://</c>, are kept whole and lowercased. No Public
    /// Suffix List is applied.
    /// </summary>
    /// <param name="value">Stored address.</param>
    /// <returns>The host key, or <see langword="null"/> when the address has no host.</returns>
    public static string? Host(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string text = value.Trim();
        var scheme = SchemeRegex().Match(text);
        if (scheme.Success)
        {
            string name = scheme.Groups["scheme"].Value;
            if (!name.Equals("http", StringComparison.OrdinalIgnoreCase) && !name.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                return text.ToLowerInvariant();
            }

            text = text[scheme.Length..];
        }
        else if (OpaqueSchemeRegex().IsMatch(text))
        {
            return text.ToLowerInvariant();
        }

        int end = text.IndexOfAny(['/', '?', '#', '\\']);
        string authority = end < 0 ? text : text[..end];
        int at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            authority = authority[(at + 1)..];
        }

        authority = StripPort(authority).TrimEnd('.').ToLowerInvariant();
        if (authority.StartsWith("www.", StringComparison.Ordinal))
        {
            authority = authority[4..];
        }

        return authority.Length == 0 ? null : authority;
    }

    /// <summary>User name key: trimmed and lowercased with the invariant culture.</summary>
    /// <param name="value">User name.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Username(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>Title key: trimmed, lowercased with the invariant culture, and with whitespace runs collapsed to one space.</summary>
    /// <param name="value">Item name.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Title(string? value) =>
        value is null ? string.Empty : WhitespaceRegex().Replace(value.Trim(), " ").ToLowerInvariant();

    /// <summary>Notes key: line endings converted to <c>\n</c> and trailing whitespace removed.</summary>
    /// <param name="value">Notes or another multi-line value.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Notes(string? value) =>
        value is null ? string.Empty : value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd();

    /// <summary>
    /// Authenticator key for comparison only: the <c>secret</c> query parameter of an <c>otpauth://</c> URI, otherwise the
    /// raw value, uppercased without spaces, <c>-</c>, or <c>=</c>. <c>steam://</c> values are kept whole and uppercased,
    /// so they compare case-insensitively. Full TOTP parsing belongs to secret detection.
    /// </summary>
    /// <param name="value">Stored authenticator key.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Totp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string text = value.Trim();
        if (text.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
        {
            return text.ToUpperInvariant();
        }

        if (text.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase) && QueryParameter(text, "secret") is { } secret)
        {
            text = secret;
        }

        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (character is not (' ' or '-' or '='))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>Address key for deduplicating stored addresses: trimmed, lowercased, without a trailing slash.</summary>
    /// <param name="value">Stored address.</param>
    /// <returns>The key.</returns>
    public static string Uri(string value) => value.Trim().TrimEnd('/').ToLowerInvariant();

    /// <summary>Whether a value counts as absent: <see langword="null"/>, empty, or whitespace only.</summary>
    /// <param name="value">Value.</param>
    /// <returns><see langword="true"/> when the value is absent.</returns>
    public static bool IsAbsent(string? value) => string.IsNullOrWhiteSpace(value);

    private static string StripPort(string authority)
    {
        if (authority.StartsWith('['))
        {
            int close = authority.IndexOf(']', StringComparison.Ordinal);
            return close < 0 ? authority : authority[..(close + 1)];
        }

        int colon = authority.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 && colon == authority.LastIndexOf(':') ? authority[..colon] : authority;
    }

    private static string? QueryParameter(string uri, string name)
    {
        int query = uri.IndexOf('?', StringComparison.Ordinal);
        if (query < 0)
        {
            return null;
        }

        int fragment = uri.IndexOf('#', query);
        string text = fragment < 0 ? uri[(query + 1)..] : uri[(query + 1)..fragment];
        foreach (string pair in text.Split('&'))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            string key = equals < 0 ? pair : pair[..equals];
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return equals < 0 ? string.Empty : System.Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(?<scheme>[A-Za-z][A-Za-z0-9+.\-]*)://", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 50)]
    private static partial Regex SchemeRegex();

    // A scheme without "//", such as mailto:x. "host:8080" is a bare host with a port, not a scheme.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:(?![0-9]+(?:[/?#\\]|$))", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 50)]
    private static partial Regex OpaqueSchemeRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 50)]
    private static partial Regex WhitespaceRegex();
}
