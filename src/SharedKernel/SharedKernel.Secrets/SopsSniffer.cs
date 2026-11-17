using System.Text;
using System.Text.Json;

namespace PandaBear.Secrets;

/// <summary>Decides from a file's content whether it carries SOPS metadata, so a sops-encrypted file with an unexpected name is still judged.</summary>
internal static class SopsSniffer
{
    /// <summary>Returns whether a file with this name could be a SOPS document: a YAML, JSON, dotenv, or ini extension.</summary>
    public static bool IsCandidateExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ini", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the format whose metadata the library reads for <paramref name="path"/>, or <see langword="null"/> for any other extension.</summary>
    public static SopsFormat? FormatOf(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
            ? SopsFormat.Yaml
            : extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ? SopsFormat.Json : null;
    }

    /// <summary>
    /// A best-effort advisory used only outside every creation rule, where a file that looks like SOPS is listed as uncovered. It is not a guarantee (go-yaml decodes
    /// tagged scalars such as <c>!!binary c29wcw==</c>, so no text search can be complete); inside a rule's scope nothing is decided by content. Decides, conservatively, whether a file could be a SOPS document. A file is <see cref="SniffResult.NotSops"/> only when it positively cannot be: it is
    /// empty, or it decodes as UTF-8 and holds no <c>sops</c> token and no <c>ENC[</c> anywhere, after YAML and JSON escapes (<c>\xNN</c>, <c>\uNNNN</c>,
    /// <c>\UNNNNNNNN</c>, line continuations) are decoded, compatibility characters are normalised (full-width letters), invisible format characters are removed,
    /// and case is folded (the long s and the like). Binary data and text that is not UTF-8 are <see cref="SniffResult.Unknown"/>. A file that does carry the
    /// token or an encrypted value is <see cref="SniffResult.Sops"/> when it parses positively as a JSON document with a top-level <c>sops</c> property or as
    /// YAML with a plain <c>sops</c> key (or as dotenv lines), and <see cref="SniffResult.Unknown"/> otherwise: a heuristic never decides that such a file is
    /// not SOPS, because go-yaml and sops read forms (complex keys, tags, anchors, comments before flow style, indented INI headers) that no line pattern covers.
    /// </summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="format">The format to read the metadata as, when the content decides it: JSON for a JSON document, YAML for a YAML key.</param>
    /// <param name="reason">Why the file cannot be ruled out, for <see cref="SniffResult.Unknown"/>.</param>
    public static SniffResult Classify(byte[] bytes, out SopsFormat? format, out string reason)
    {
        format = null;
        reason = string.Empty;
        if (bytes.Length == 0)
        {
            return SniffResult.NotSops;
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            reason = "it is binary or not UTF-8";
            return SniffResult.Unknown;
        }

        if (text.Contains('\0', StringComparison.Ordinal))
        {
            reason = "it is binary";
            return SniffResult.Unknown;
        }

        if (!HasToken(text))
        {
            return SniffResult.NotSops;
        }

        var trimmed = text.TrimStart('\uFEFF').TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '{' or '[')
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.EnumerateObject().Any(static p => string.Equals(p.Name, "sops", StringComparison.OrdinalIgnoreCase)))
                {
                    format = SopsFormat.Json;
                    return SniffResult.Sops;
                }
            }
            catch (JsonException)
            {
                // Falls through: JSON-looking text that mentions sops and does not parse is not ruled out.
            }
        }
        else
        {
            var lines = text.TrimStart('\uFEFF').ReplaceLineEndings("\n").Split('\n');
            if (lines.Any(IsSopsKeyLine))
            {
                format = SopsFormat.Yaml;
                return SniffResult.Sops;
            }

            if (lines.Any(static line => line.StartsWith("sops_", StringComparison.OrdinalIgnoreCase)))
            {
                return SniffResult.Sops;
            }
        }

        reason = "it mentions sops or holds an encrypted value, but is not a document this tool can read";
        return SniffResult.Unknown;
    }

    /// <summary>Returns whether the text, escapes decoded and case folded, holds <c>sops</c> or <c>ENC[</c> anywhere.</summary>
    internal static bool HasToken(string text) => Contains(Fold(text, unescape: false)) || Contains(Fold(text, unescape: true));

    /// <summary>
    /// A separator-insensitive search of folded text for <c>s o p s</c> or <c>e n c [</c> with anything that is not a letter or digit between the characters
    /// (the pattern <c>s[^\p{L}\p{N}]*o[^\p{L}\p{N}]*p[^\p{L}\p{N}]*s</c>, and <c>e..n..c</c> then separators then <c>[</c>). It runs in linear time: only letters and digits are kept,
    /// and the bracket is looked for among the separators that follow the <c>c</c>. This is a backstop for escape equivalences that the decoder does not list, so
    /// it over-flags on purpose: <c>s.o.p.s</c> in prose counts.
    /// </summary>
    private static bool Contains(string folded)
    {
        List<int> positions = [];
        StringBuilder letters = new();
        for (var index = 0; index < folded.Length; index++)
        {
            if (char.IsLetterOrDigit(folded[index]))
            {
                letters.Append(folded[index]);
                positions.Add(index);
            }
        }

        var compact = letters.ToString();
        if (compact.Contains("SOPS", StringComparison.Ordinal))
        {
            return true;
        }

        for (var start = compact.IndexOf("ENC", StringComparison.Ordinal); start >= 0; start = compact.IndexOf("ENC", start + 1, StringComparison.Ordinal))
        {
            var last = positions[start + 2];
            var next = start + 3 < positions.Count ? positions[start + 3] : folded.Length;
            if (folded.AsSpan(last + 1, next - last - 1).Contains('['))
            {
                return true;
            }
        }

        return false;
    }

    private static string Fold(string text, bool unescape)
    {
        var decoded = unescape ? UnescapeText(text) : text;
        try
        {
            decoded = decoded.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            // Not normalisable (a lone surrogate): fold it as it is.
        }

        StringBuilder folded = new(decoded.Length);
        foreach (var rune in decoded.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) != System.Globalization.UnicodeCategory.Format)
            {
                folded.Append(Rune.ToUpperInvariant(rune));
            }
        }

        return folded.ToString();
    }

    /// <summary>The line breaks libyaml recognises: line feed, carriage return, next line (U+0085), line separator (U+2028), and paragraph separator (U+2029).</summary>
    private static bool IsBreak(char value) => value is '\n' or '\r' or '\u0085' or '\u2028' or '\u2029';

    /// <summary>Decodes <c>\xHH</c>, <c>\uHHHH</c>, <c>\UHHHHHHHH</c> and a backslash that ends a line (with the indentation that follows it) anywhere in the text.</summary>
    private static string UnescapeText(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        StringBuilder decoded = new(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index + 1 >= text.Length)
            {
                decoded.Append(text[index]);
                continue;
            }

            var next = text[index + 1];
            if (IsBreak(next))
            {
                index++;
                while (index + 1 < text.Length && (IsBreak(text[index + 1]) || text[index + 1] is ' ' or '\t'))
                {
                    index++;
                }

                continue;
            }

            var width = next switch
            {
                'x' => 2,
                'u' => 4,
                'U' => 8,
                _ => 0
            };
            if (width > 0 && index + 2 + width <= text.Length && int.TryParse(text.AsSpan(index + 2, width), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code) && Rune.IsValid(code))
            {
                decoded.Append(char.ConvertFromUtf32(code));
                index += 1 + width;
            }
            else
            {
                decoded.Append(text[index]);
            }
        }

        return decoded.ToString();
    }

    private static bool IsSopsKeyLine(string line)
    {
        var text = line.TrimStart(' ', '\t');
        while (text.StartsWith("- ", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart(' ');
        }

        if (text.Length == 0 || text[0] == '#')
        {
            return false;
        }

        var colon = FindMappingColon(text);
        return colon > 0 && string.Equals(Unescape(text[..colon].Trim(' ', '\t', '"', '\'')), "sops", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Decodes the YAML escapes <c>\xHH</c>, <c>\uHHHH</c>, and <c>\UHHHHHHHH</c>, so <c>"s\x6fps"</c> reads as <c>sops</c>.</summary>
    private static string Unescape(string key)
    {
        if (!key.Contains('\\', StringComparison.Ordinal))
        {
            return key;
        }

        StringBuilder decoded = new();
        for (var index = 0; index < key.Length; index++)
        {
            var width = key[index] == '\\' && index + 1 < key.Length ? key[index + 1] switch
            {
                'x' => 2,
                'u' => 4,
                'U' => 8,
                _ => 0
            } : 0;
            if (width > 0 && index + 2 + width <= key.Length && int.TryParse(key.AsSpan(index + 2, width), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code) && Rune.IsValid(code))
            {
                decoded.Append(char.ConvertFromUtf32(code));
                index += 1 + width;
            }
            else
            {
                decoded.Append(key[index]);
            }
        }

        return decoded.ToString();
    }

    /// <summary>Finds the colon that ends a mapping key: outside quotes, followed by a space or the end of the line.</summary>
    private static int FindMappingColon(string text)
    {
        var start = 0;
        if (text[0] is '"' or '\'')
        {
            var close = text.IndexOf(text[0], 1, StringComparison.Ordinal);
            if (close < 0)
            {
                return -1;
            }

            start = close;
        }

        for (var index = Math.Max(start, 1); index < text.Length; index++)
        {
            if (text[index] == ':' && (index + 1 == text.Length || text[index + 1] == ' '))
            {
                return index;
            }
        }

        return -1;
    }
}
