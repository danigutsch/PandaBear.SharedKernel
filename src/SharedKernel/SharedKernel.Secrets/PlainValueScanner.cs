using System.Text.Json;

namespace SharedKernel.Secrets;

/// <summary>
/// Counts the data values of a SOPS file that are not encrypted and notices comments. sops writes every string, number, and boolean as a
/// complete <c>ENC[AES256_GCM,data:...,iv:...,tag:...,type:...]</c>; an empty string, <c>null</c>, and an empty list or mapping stay as they
/// are. Keys that end with the default <c>_unencrypted</c> suffix (and everything below them) are meant to stay in clear and are not examined. Any
/// comment text outside the <c>sops</c> block is reported, because a comment is not covered by the checks and can hold anything.
/// </summary>
internal static class PlainValueScanner
{
    private const string Suffix = "_unencrypted";
    private const int InitializationVectorLength = 32;
    private const int AuthenticationTagLength = 16;

    private static readonly HashSet<string> ValueTypes = new(StringComparer.Ordinal) { "str", "int", "float", "bool", "bytes", "comment" };

    /// <summary>Counts plain values in the lines outside the <c>sops</c> block of a YAML file, and says whether any comment has text.</summary>
    internal static int CountYaml(IEnumerable<string> lines, out bool hasComment)
    {
        hasComment = false;
        var count = 0;
        var skipBelow = -1;
        var blockBelow = -1;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart(' ');
            if (trimmed.Length == 0)
            {
                continue;
            }

            var indent = line.Length - trimmed.Length;
            if (blockBelow >= 0 && indent > blockBelow)
            {
                continue;
            }

            blockBelow = -1;
            if (skipBelow >= 0 && indent > skipBelow)
            {
                continue;
            }

            skipBelow = -1;
            if (trimmed[0] == '#')
            {
                hasComment |= trimmed[1..].Trim().Length > 0;
                continue;
            }

            var content = indent;
            while (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed == "-")
            {
                var after = trimmed.Length == 1 ? string.Empty : trimmed[1..].TrimStart(' ');
                content += trimmed.Length - after.Length;
                trimmed = after;
            }

            if (trimmed.Length == 0)
            {
                continue;
            }

            var value = trimmed;
            var colon = trimmed[0] is '[' or '{' ? -1 : SopsYamlSubset.FindMappingColon(trimmed);
            if (colon >= 0)
            {
                var key = trimmed[..colon].Trim(' ', '"', '\'');
                if (key.EndsWith(Suffix, StringComparison.Ordinal))
                {
                    skipBelow = content;
                    continue;
                }

                value = trimmed[(colon + 1)..].Trim(' ');
            }

            if (value.Length > 0 && value[0] is '|' or '>')
            {
                blockBelow = content;
                count++;
                continue;
            }

            value = SplitComment(value, out var comment);
            hasComment |= comment;
            if (!IsEncryptedOrEmpty(value))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Counts plain values in a JSON document, not looking into the <c>sops</c> property of the root.</summary>
    internal static int CountJson(JsonElement root)
    {
        var count = 0;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject().Where(static p => !p.NameEquals("sops")))
            {
                count += Count(property);
            }
        }

        return count;
    }

    /// <summary>Returns whether <paramref name="value"/> is a complete value as sops writes it: the cipher, base64 data, a 32-byte iv, a 16-byte tag, and a known type.</summary>
    internal static bool IsWellFormedEncryptedValue(string value)
    {
        if (!value.StartsWith("ENC[", StringComparison.Ordinal) || !value.EndsWith(']', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value[4..^1].Split(',');
        return parts.Length == 5
            && parts[0] == "AES256_GCM"
            && TryBase64(parts[1], "data:", out _)
            && TryBase64(parts[2], "iv:", out var iv) && iv == InitializationVectorLength
            && TryBase64(parts[3], "tag:", out var tag) && tag == AuthenticationTagLength
            && parts[4].StartsWith("type:", StringComparison.Ordinal) && ValueTypes.Contains(parts[4][5..]);
    }

    private static bool TryBase64(string part, string prefix, out int length)
    {
        length = 0;
        if (!part.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var text = part[prefix.Length..];
        if (!text.All(static c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '='))
        {
            return false;
        }

        try
        {
            length = Convert.FromBase64String(text).Length;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static int Count(JsonProperty property) => property.Name.EndsWith(Suffix, StringComparison.Ordinal) ? 0 : CountValue(property.Value);

    private static int CountValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Sum(Count),
        JsonValueKind.Array => value.EnumerateArray().Sum(CountValue),
        JsonValueKind.Null => 0,
        JsonValueKind.String => IsEncryptedOrEmpty(value.GetString() ?? string.Empty) ? 0 : 1,
        _ => 1,
    };

    /// <summary>Splits an inline comment off a YAML value, outside quotes. A comment counts when it has text after the marker.</summary>
    private static string SplitComment(string value, out bool hasComment)
    {
        hasComment = false;
        if (value.Length > 0 && value[0] == '#')
        {
            hasComment = value[1..].Trim().Length > 0;
            return string.Empty;
        }

        var end = value.Length;
        var tail = string.Empty;
        if (value.Length > 0 && value[0] is '"' or '\'')
        {
            var close = value.IndexOf(value[0], 1, StringComparison.Ordinal);
            if (close >= 0)
            {
                end = close + 1;
                tail = value[end..].Trim();
            }
        }
        else
        {
            var comment = value.IndexOf(" #", StringComparison.Ordinal);
            if (comment >= 0)
            {
                end = comment;
                tail = value[(comment + 1)..];
            }
        }

        hasComment = tail.Length > 1 && tail[0] == '#' && tail[1..].Trim().Length > 0;
        return value[..end].TrimEnd(' ');
    }

    private static bool IsEncryptedOrEmpty(string value) =>
        value is "" or "null" or "~" or "[]" or "{}" or "\"\"" or "''" || IsWellFormedEncryptedValue(value);
}
