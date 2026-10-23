using System.Diagnostics.CodeAnalysis;
using System.Text;
using PandaBear.InputNormalization;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Matching;

/// <summary>
/// Comparison keys for item values. Keys are used to compare and group values; they are never written to the output.
/// </summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Hosts, user names, and titles are specified as lowercase keys; they are compared ordinally and never displayed or written.")]
internal static class ItemNormalizer
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
        int schemeLength = SchemeLength(text);
        if (schemeLength > 0 && text.AsSpan(schemeLength).StartsWith("://", StringComparison.Ordinal))
        {
            ReadOnlySpan<char> name = text.AsSpan(0, schemeLength);
            if (!name.Equals("http", StringComparison.OrdinalIgnoreCase) && !name.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                return text.ToLowerInvariant();
            }

            text = text[(schemeLength + "://".Length)..];
        }
        else if (HasOpaqueScheme(text, schemeLength))
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

    /// <summary>
    /// Title key: <see cref="StringSanitizer.Sanitize"/> output (Unicode normalization form C, control characters removed,
    /// trimmed, and whitespace runs collapsed to one space), lowercased with the invariant culture.
    /// </summary>
    /// <param name="value">Item name.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Title(string? value) =>
        value is null ? string.Empty : StringSanitizer.Sanitize(value).ToLowerInvariant();

    /// <summary>Notes key: line endings converted to <c>\n</c> and trailing whitespace removed.</summary>
    /// <param name="value">Notes or another multi-line value.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Notes(string? value) =>
        value is null ? string.Empty : value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd();

    /// <summary>
    /// Authenticator key for comparison only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value that <see cref="TotpUri.TryNormalize"/> accepts compares by what its codes depend on: the normalized secret
    /// plus any algorithm, digit count, or period other than the defaults. A plain base32 secret therefore equals an
    /// <c>otpauth://</c> URI with default settings, issuer and label differences are ignored, and different code settings
    /// make different keys. Accepted <c>steam://</c> values compare by their normalized secret.
    /// </para>
    /// <para>
    /// Other values compare leniently: the <c>secret</c> query parameter of an <c>otpauth://</c> URI, otherwise the raw
    /// value, uppercased without spaces, <c>-</c>, or <c>=</c>. Other <c>steam://</c> values are kept whole and uppercased.
    /// </para>
    /// </remarks>
    /// <param name="value">Stored authenticator key.</param>
    /// <returns>The key; empty when the value is absent.</returns>
    public static string Totp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (TotpUri.TryNormalize(value, null, null, out string? normalized) && normalized is not null)
        {
            return TotpSettingsKey(normalized);
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

    // Normalized URIs put the secret first and omit default settings, so dropping the label and issuer leaves only what
    // the generated codes depend on. Normalized steam:// values have no query.
    private static string TotpSettingsKey(string normalized)
    {
        int query = normalized.IndexOf('?', StringComparison.Ordinal);
        if (query < 0)
        {
            return normalized.ToUpperInvariant();
        }

        var key = new StringBuilder();
        foreach (string parameter in normalized[(query + 1)..].Split('&'))
        {
            if (parameter.StartsWith("secret=", StringComparison.Ordinal))
            {
                key.Insert(0, parameter["secret=".Length..]);
            }
            else if (!parameter.StartsWith("issuer=", StringComparison.Ordinal))
            {
                key.Append('&').Append(parameter);
            }
        }

        return key.ToString();
    }

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

    // Length of the scheme name at the start of a URI, an ASCII letter followed by ASCII letters, digits, "+", ".", or
    // "-", or 0 when the text does not start with one. The scan uses no pattern, so computing an origin key has no match
    // timeout.
    private static int SchemeLength(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetter(text[0]))
        {
            return 0;
        }

        int length = 1;
        while (length < text.Length && (char.IsAsciiLetterOrDigit(text[length]) || text[length] is '+' or '.' or '-'))
        {
            length++;
        }

        return length;
    }

    // A scheme without "//", such as mailto:x. "host:8080" is a bare host with a port, not a scheme: the colon is followed
    // by digits that end the text or end before "/", "?", "#", or "\".
    private static bool HasOpaqueScheme(string text, int schemeLength)
    {
        if (schemeLength == 0 || schemeLength == text.Length || text[schemeLength] != ':')
        {
            return false;
        }

        int digitsEnd = schemeLength + 1;
        while (digitsEnd < text.Length && char.IsAsciiDigit(text[digitsEnd]))
        {
            digitsEnd++;
        }

        bool port = digitsEnd > schemeLength + 1 && (digitsEnd == text.Length || text[digitsEnd] is '/' or '?' or '#' or '\\');
        return !port;
    }
}
