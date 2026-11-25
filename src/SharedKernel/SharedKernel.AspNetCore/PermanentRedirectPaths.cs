using System.Buffers;
using System.Globalization;
using System.Text;

namespace SharedKernel.AspNetCore;

/// <summary>
/// The canonical form of a moved path, so the path a redirect is stored under and the path a request asks for meet whatever their case,
/// trailing slash, escaping or Unicode composition; and the percent-encoded form of a target that <see cref="PermanentRedirectExtensions.UsePermanentRedirects"/> accepts.
/// </summary>
public static class PermanentRedirectPaths
{
    /// <summary>The longest percent-encoding of one UTF-16 character: three UTF-8 bytes of three characters each.</summary>
    private const int MaxEncodedCharactersPerCharacter = 9;

    /// <summary>
    /// Normalizes a path into its canonical form: trimmed, percent-decoded once, Unicode NFC (an accent typed as one character or as a
    /// letter plus a combining mark is the same path), lower case, without trailing <c>/</c>. Use it on the path a redirect is stored under
    /// and on the request path it is looked up by; a canonical path normalizes to itself.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> is returned, so nothing is matched, for the root, for anything that does not start with a single <c>/</c>, for an
    /// encoded <c>/</c> (<c>%2F</c>), for an escape still left after decoding (double encoding), for broken UTF-8 or UTF-16 (a lone
    /// surrogate or the replacement character U+FFFD), for a control, format, separator or white-space character, for <c>?</c>, <c>#</c> or <c>\</c>, for an empty, <c>.</c> or <c>..</c> segment, for a
    /// canonical path longer than <paramref name="maxLength"/>, and, before any decoding, for an input longer than nine times
    /// <paramref name="maxLength"/> (the longest encoding of a canonical path of that length).
    /// </remarks>
    /// <param name="path">The path, decoded (as <see cref="Microsoft.AspNetCore.Http.HttpRequest.Path"/> gives it) or percent-encoded (as a browser shows it).</param>
    /// <param name="maxLength">The longest canonical path, in UTF-16 characters, such as the length of the column it is stored in.</param>
    /// <returns>The canonical path, or <see langword="null"/> when the path is refused.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than 2.</exception>
    public static string? Normalize(string? path, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 2);

        var candidate = path?.Trim();
        if (candidate is null
            || candidate.Length < 2
            || candidate[0] != '/'
            || candidate.Length > (long)maxLength * MaxEncodedCharactersPerCharacter
            || candidate.Contains("%2F", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var decoded = Uri.UnescapeDataString(candidate);
        if (!IsPlainText(decoded) || HasEscape(decoded))
        {
            return null;
        }

        var canonical = decoded.Normalize(NormalizationForm.FormC).TrimEnd('/');
        if (canonical.Length == 0
            || canonical.Length > maxLength
            || canonical.AsSpan().IndexOfAny('?', '#', '\\') >= 0
            || canonical[1..].Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return null;
        }

#pragma warning disable CA1308 // Moved paths are compared in lower case, the form sites emit; the result is a lookup key, not a security decision.
        return canonical.ToLowerInvariant();
#pragma warning restore CA1308
    }

    /// <summary>
    /// Percent-encodes every segment of a canonical path, so it is the plain ASCII local path a <see cref="PermanentRedirect"/> target and a
    /// <c>Location</c> header need. <see cref="Normalize"/> turns the result back into the canonical path.
    /// </summary>
    /// <param name="path">The canonical path, such as <c>/pt/páginas/história</c>.</param>
    /// <returns>The encoded path, such as <c>/pt/p%C3%A1ginas/hist%C3%B3ria</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static string Encode(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
    }

    private static bool IsPlainText(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var consumed) != OperationStatus.Done
                || rune == Rune.ReplacementChar
                || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                    or UnicodeCategory.Format
                    or UnicodeCategory.SpaceSeparator
                    or UnicodeCategory.LineSeparator
                    or UnicodeCategory.ParagraphSeparator)
            {
                return false;
            }

            index += consumed;
        }

        return true;
    }

    private static bool HasEscape(string text)
    {
        for (var index = 0; index + 2 < text.Length; index++)
        {
            if (text[index] == '%' && char.IsAsciiHexDigit(text[index + 1]) && char.IsAsciiHexDigit(text[index + 2]))
            {
                return true;
            }
        }

        return false;
    }
}
