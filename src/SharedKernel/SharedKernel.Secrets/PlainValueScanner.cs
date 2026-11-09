using System.Text.Json;

namespace PandaBear.Secrets;

/// <summary>
/// Counts the data values of a SOPS file that are not encrypted. sops writes every string, number, and boolean as <c>ENC[...]</c>; an empty
/// string, <c>null</c>, and an empty list or mapping stay as they are. Keys that end with the default <c>_unencrypted</c> suffix (and everything
/// below them) are meant to stay in clear and are not counted. A comment that is not encrypted counts.
/// </summary>
internal static class PlainValueScanner
{
    private const string Suffix = "_unencrypted";

    /// <summary>Counts plain values in the lines outside the <c>sops</c> block of a YAML file.</summary>
    internal static int CountYaml(IEnumerable<string> lines)
    {
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
                count += trimmed.StartsWith("#ENC[", StringComparison.Ordinal) ? 0 : 1;
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

            if (!IsEncryptedOrEmpty(StripComment(value)))
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

    private static int Count(JsonProperty property) => property.Name.EndsWith(Suffix, StringComparison.Ordinal) ? 0 : CountValue(property.Value);

    private static int CountValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Sum(Count),
        JsonValueKind.Array => value.EnumerateArray().Sum(CountValue),
        JsonValueKind.Null => 0,
        JsonValueKind.String => IsEncryptedOrEmpty(value.GetString() ?? string.Empty) ? 0 : 1,
        _ => 1,
    };

    private static string StripComment(string value)
    {
        if (value.Length > 0 && value[0] is '"' or '\'')
        {
            var close = value.IndexOf(value[0], 1);
            return close < 0 ? value : value[..(close + 1)];
        }

        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return (comment >= 0 ? value[..comment] : value).TrimEnd(' ');
    }

    private static bool IsEncryptedOrEmpty(string value) =>
        value is "" or "null" or "~" or "[]" or "{}" or "\"\"" or "''"
        || (value.StartsWith("ENC[", StringComparison.Ordinal) && value.EndsWith(']'));
}
