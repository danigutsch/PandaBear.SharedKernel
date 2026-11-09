using System.Text;
using System.Text.Json;

namespace PandaBear.Secrets.Tool;

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
    /// Looks at the bytes of a file. Binary data, text that is not UTF-8, and JSON-looking text that does not parse cannot be ruled out; JSON with a top-level
    /// <c>sops</c> property (any letter case, escapes decoded), YAML with a <c>sops</c> key at any indentation (quotes and escapes decoded), and dotenv or ini
    /// SOPS lines are SOPS documents; everything else is text that holds no SOPS metadata.
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

        text = text.TrimStart('\uFEFF');
        var trimmed = text.TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '{' or '[')
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var has = document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.EnumerateObject().Any(static p => string.Equals(p.Name, "sops", StringComparison.OrdinalIgnoreCase));
                format = has ? SopsFormat.Json : null;
                return has ? SniffResult.Sops : SniffResult.NotSops;
            }
            catch (JsonException)
            {
                reason = "it looks like JSON but does not parse";
                return SniffResult.Unknown;
            }
        }

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines.Any(IsSopsKeyLine))
        {
            format = SopsFormat.Yaml;
            return SniffResult.Sops;
        }

        return lines.Any(static line => line.StartsWith("sops_", StringComparison.OrdinalIgnoreCase) || line.StartsWith("[sops", StringComparison.OrdinalIgnoreCase))
            ? SniffResult.Sops
            : SniffResult.NotSops;
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
            var close = text.IndexOf(text[0], 1);
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
