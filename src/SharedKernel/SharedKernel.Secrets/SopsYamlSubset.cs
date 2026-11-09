using System.Text;
using System.Text.Json;

namespace PandaBear.Secrets;

/// <summary>
/// Converts the <c>sops:</c> block of a SOPS-written YAML file into JSON. It understands block mappings, block sequences,
/// plain and quoted scalars, block scalars (which are skipped, as they only hold ciphertext), and empty flow collections.
/// Anything else, a tab indent, a repeated key, or a second <c>sops:</c> block yields <see langword="null"/>.
/// </summary>
internal static class SopsYamlSubset
{
    private const int MaxDepth = 16;

    /// <summary>Returns the JSON object <c>{"sops": ...}</c>, or <see langword="null"/> when the text is outside the subset.</summary>
    internal static string? ToJson(string yaml)
    {
        var lines = yaml.ReplaceLineEndings("\n").Split('\n');
        var start = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            if (!IsTopLevelSops(lines[index]))
            {
                continue;
            }

            if (start >= 0)
            {
                return null;
            }

            start = index;
        }

        if (start < 0 || !IsEmptyValue(lines[start]))
        {
            return null;
        }

        List<Token> tokens = [];
        for (var index = start + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.TrimStart(' ');
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            if (line[0] != ' ')
            {
                break;
            }

            if (trimmed[0] == '\t')
            {
                return null;
            }

            tokens.Add(new Token(line.Length - trimmed.Length, trimmed));
        }

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("sops");
            var parser = new Parser(tokens, writer);
            if (tokens.Count == 0 || !parser.ParseBlock(tokens[0].Indent, 0) || !parser.AtEnd)
            {
                return null;
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool IsTopLevelSops(string line) =>
        line.StartsWith("sops:", StringComparison.Ordinal) || line.StartsWith("\"sops\":", StringComparison.Ordinal) || line.StartsWith("'sops':", StringComparison.Ordinal);

    private static bool IsEmptyValue(string line)
    {
        var rest = line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim();
        return rest.Length == 0 || rest[0] == '#';
    }

    private readonly record struct Token(int Indent, string Text);

    private sealed class Parser(List<Token> tokens, Utf8JsonWriter writer)
    {
        private int _position;

        internal bool AtEnd => _position == tokens.Count;

        internal bool ParseBlock(int indent, int depth)
        {
            if (depth > MaxDepth || _position >= tokens.Count || tokens[_position].Indent != indent)
            {
                return false;
            }

            return IsSequenceItem(tokens[_position].Text) ? ParseSequence(indent, depth) : ParseMapping(indent, depth);
        }

        private static bool IsSequenceItem(string text) => text == "-" || text.StartsWith("- ", StringComparison.Ordinal);

        private bool ParseSequence(int indent, int depth)
        {
            writer.WriteStartArray();
            while (_position < tokens.Count && tokens[_position].Indent == indent && IsSequenceItem(tokens[_position].Text))
            {
                var token = tokens[_position];
                var rest = token.Text.Length == 1 ? string.Empty : token.Text[1..].TrimStart(' ');
                if (rest.Length == 0 || rest[0] == '#')
                {
                    _position++;
                    if (_position >= tokens.Count || tokens[_position].Indent <= indent || !ParseBlock(tokens[_position].Indent, depth + 1))
                    {
                        return false;
                    }

                    continue;
                }

                if (FindMappingColon(rest) >= 0)
                {
                    var contentIndent = token.Indent + (token.Text.Length - rest.Length);
                    tokens[_position] = new Token(contentIndent, rest);
                    if (!ParseMapping(contentIndent, depth + 1))
                    {
                        return false;
                    }

                    continue;
                }

                _position++;
                if (!WriteScalar(rest))
                {
                    return false;
                }
            }

            writer.WriteEndArray();
            return true;
        }

        private bool ParseMapping(int indent, int depth)
        {
            writer.WriteStartObject();
            HashSet<string> keys = new(StringComparer.Ordinal);
            while (_position < tokens.Count && tokens[_position].Indent == indent && !IsSequenceItem(tokens[_position].Text))
            {
                var text = tokens[_position].Text;
                var colon = FindMappingColon(text);
                if (colon < 1)
                {
                    return false;
                }

                var key = Unquote(text[..colon].TrimEnd(' '));
                if (key is null || !keys.Add(key))
                {
                    return false;
                }

                writer.WritePropertyName(key);
                var rest = text[(colon + 1)..].Trim(' ');
                _position++;
                if (!ParseValue(rest, indent, depth))
                {
                    return false;
                }
            }

            writer.WriteEndObject();
            return true;
        }

        private bool ParseValue(string rest, int indent, int depth)
        {
            if (rest.Length == 0 || rest[0] == '#')
            {
                if (_position < tokens.Count && (tokens[_position].Indent > indent || (tokens[_position].Indent == indent && IsSequenceItem(tokens[_position].Text))))
                {
                    return ParseBlock(tokens[_position].Indent, depth + 1);
                }

                writer.WriteNullValue();
                return true;
            }

            if (rest[0] is '|' or '>')
            {
                // A block scalar holds ciphertext here; skip its lines and keep no content.
                while (_position < tokens.Count && tokens[_position].Indent > indent)
                {
                    _position++;
                }

                writer.WriteStringValue(string.Empty);
                return true;
            }

            return WriteScalar(rest);
        }

        private bool WriteScalar(string raw)
        {
            if (raw is "[]")
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
                return true;
            }

            if (raw is "{}")
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
                return true;
            }

            if (raw[0] is '[' or '{' or '&' or '*' or '!' or '|' or '>' or '%' or '@' or '`')
            {
                return false;
            }

            if (raw[0] is '"' or '\'')
            {
                var unquoted = Unquote(raw);
                if (unquoted is null)
                {
                    return false;
                }

                writer.WriteStringValue(unquoted);
                return true;
            }

            var comment = raw.IndexOf(" #", StringComparison.Ordinal);
            var plain = (comment >= 0 ? raw[..comment] : raw).TrimEnd(' ');
            if (plain is "null" or "~")
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(plain);
            }

            return true;
        }

        private static string? Unquote(string text)
        {
            if (text.Length == 0)
            {
                return null;
            }

            if (text[0] is not ('"' or '\''))
            {
                return text;
            }

            var close = text.IndexOf(text[0], 1);
            if (close < 0 || (text[0] == '"' && text.Contains('\\', StringComparison.Ordinal)))
            {
                return null;
            }

            var after = text[(close + 1)..].TrimStart(' ');
            return after.Length == 0 || after[0] == '#' ? text[1..close] : null;
        }

        private static int FindMappingColon(string text)
        {
            if (text[0] is '"' or '\'')
            {
                var close = text.IndexOf(text[0], 1);
                return close >= 0 && close + 1 < text.Length && text[close + 1] == ':' && (close + 2 == text.Length || text[close + 2] == ' ') ? close + 1 : -1;
            }

            if (text[0] is '[' or '{')
            {
                return -1;
            }

            for (var index = 1; index < text.Length; index++)
            {
                if (text[index] == ':' && (index + 1 == text.Length || text[index + 1] == ' '))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
