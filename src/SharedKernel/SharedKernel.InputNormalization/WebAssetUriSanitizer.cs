using System.Text;

namespace PandaBear.InputNormalization;

/// <summary>
/// Normalizes optional web asset URI values for safe storage and rendering.
/// </summary>
public static class WebAssetUriSanitizer
{
    private static readonly (string Prefix, byte?[] Signature)[] RasterDataUriSignatures =
    [
        ("data:image/png;base64,", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ("data:image/jpeg;base64,", [0xFF, 0xD8, 0xFF]),
        ("data:image/webp;base64,", [0x52, 0x49, 0x46, 0x46, null, null, null, null, 0x57, 0x45, 0x42, 0x50]),
    ];

    /// <summary>
    /// Normalizes a root-relative or absolute HTTPS web asset URI string.
    /// </summary>
    /// <param name="value">The candidate web asset URI.</param>
    /// <param name="maxLength">The maximum accepted URI length.</param>
    /// <returns>The normalized URI string, or <see langword="null" /> when missing or unsafe.</returns>
    public static string? NormalizeRootRelativeOrHttps(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        if (ContainsControlCharacter(value))
        {
            return null;
        }

        var normalized = value.Normalize(NormalizationForm.FormC);
        var trimmed = normalized.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength
            || ContainsWhitespace(trimmed)
            || trimmed.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        if (trimmed[0] == '/')
        {
            return trimmed.StartsWith("//", StringComparison.Ordinal) ? null : trimmed;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo)
            && !string.IsNullOrWhiteSpace(uri.Host)
                ? trimmed
                : null;
    }

    /// <summary>
    /// Converts a safe root-relative or absolute HTTPS web asset URI string to a URI value.
    /// </summary>
    /// <param name="value">The candidate web asset URI.</param>
    /// <param name="maxLength">The maximum accepted URI length.</param>
    /// <returns>The URI value, or <see langword="null" /> when missing or unsafe.</returns>
    public static Uri? ToRootRelativeOrHttpsUri(string? value, int maxLength)
    {
        var normalized = NormalizeRootRelativeOrHttps(value, maxLength);
        return normalized is null ? null : new Uri(normalized, UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// Returns the value unchanged when it is a base64 <c>data:</c> URI of a PNG, JPEG or WebP image whose decoded
    /// bytes start with the signature of the declared type; otherwise <see langword="null" />.
    /// </summary>
    /// <remarks>
    /// The WebP signature is a RIFF header carrying the WEBP tag. SVG and every other type are rejected because they
    /// can carry script or external references, as are parameters, whitespace, unpadded or malformed base64, and values
    /// longer than the limit. The check proves only the type signature; an application that accepts uploads must still
    /// decode and re-encode the image itself.
    /// </remarks>
    /// <param name="value">The candidate URI.</param>
    /// <param name="maxLength">The maximum accepted length of the whole URI.</param>
    /// <returns>The accepted URI, or <see langword="null" />.</returns>
    public static string? NormalizeRasterDataImage(string? value, int maxLength)
    {
        if (value is null || value.Length > maxLength)
        {
            return null;
        }

        foreach (var (prefix, signature) in RasterDataUriSignatures)
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return HasSignature(value.AsSpan(prefix.Length), signature) ? value : null;
            }
        }

        return null;
    }

    private static bool HasSignature(ReadOnlySpan<char> base64, byte?[] signature)
    {
        if (base64.IsEmpty || base64.Length % 4 != 0 || !IsStrictBase64(base64))
        {
            return false;
        }

        var bytes = new byte[base64.Length / 4 * 3];
        if (!Convert.TryFromBase64Chars(base64, bytes, out var written) || written < signature.Length)
        {
            return false;
        }

        for (var index = 0; index < signature.Length; index++)
        {
            if (signature[index] is { } expected && bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStrictBase64(ReadOnlySpan<char> value)
    {
        var padding = value.EndsWith("==", StringComparison.Ordinal) ? 2 : value.EndsWith('=') ? 1 : 0;
        foreach (var character in value[..^padding])
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('+' or '/'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsControlCharacter(string value) => value.Any(char.IsControl);

    private static bool ContainsWhitespace(string value) => value.Any(char.IsWhiteSpace);
}
