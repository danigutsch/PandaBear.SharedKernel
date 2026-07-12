using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SharedKernel.InputNormalization;

/// <summary>
/// Normalizes optional web asset URI values for safe storage and rendering.
/// </summary>
public static class WebAssetUriSanitizer
{
    /// <summary>
    /// Normalizes a root-relative or absolute HTTPS web asset URI string.
    /// </summary>
    /// <param name="value">The candidate web asset URI.</param>
    /// <param name="maxLength">The maximum accepted URI length.</param>
    /// <returns>The normalized URI string, or <see langword="null" /> when missing or unsafe.</returns>
    [SuppressMessage("Design", "CA1055:URI-like return values should not be strings", Justification = "Web assets support root-relative paths and absolute HTTPS URIs.")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Web assets support root-relative paths and absolute HTTPS URIs.")]
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
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Web assets support root-relative paths and absolute HTTPS URIs.")]
    public static Uri? ToRootRelativeOrHttpsUri(string? value, int maxLength)
    {
        var normalized = NormalizeRootRelativeOrHttps(value, maxLength);
        return normalized is null ? null : new Uri(normalized, UriKind.RelativeOrAbsolute);
    }

    private static bool ContainsControlCharacter(string value) => value.Any(char.IsControl);

    private static bool ContainsWhitespace(string value) => value.Any(char.IsWhiteSpace);
}
