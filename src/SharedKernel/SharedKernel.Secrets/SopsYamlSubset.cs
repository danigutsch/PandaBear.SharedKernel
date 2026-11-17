using System.Text;
using System.Text.Json;

namespace PandaBear.Secrets;

/// <summary>
/// Converts the <c>sops:</c> block of a SOPS-written YAML file into JSON. It understands block mappings, block sequences,
/// plain and quoted scalars, literal block scalars (kept as text, for the armored age files), and empty flow collections.
/// Anything else, a tab indent, a repeated key, or a second <c>sops:</c> block yields <see langword="null"/>.
/// </summary>
internal static class SopsYamlSubset
{
    private const int MaxDepth = 16;

    /// <summary>
    /// Returns the JSON object <c>{"sops": ...}</c> and the lines outside the <c>sops</c> block, or <see langword="null"/> when the text is outside
    /// the subset. It is deliberately strict about the top level: a byte order mark, a document marker or directive, a tab indent, a top-level
    /// anchor, alias, tag, flow collection, or sequence, and any top-level key that reads as <c>sops</c> without being spelled <c>sops:</c> exactly
    /// all fail, so a fake block cannot hide the real one.
    /// </summary>
    internal static (string Json, List<string> DataLines)? Read(string yaml)
    {
        if (yaml.Contains('\uFEFF', StringComparison.Ordinal))
        {
            return null;
        }

        var lines = yaml.ReplaceLineEndings("\n").Split('\n');
        var start = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            var kind = ClassifyTopLevel(lines[index]);
            if (kind == TopLevel.Invalid)
            {
                return null;
            }

            if (kind != TopLevel.Sops)
            {
                continue;
            }

            if (start >= 0)
            {
                return null;
            }

            start = index;
        }

        if (start < 0)
        {
            return null;
        }

        List<Token> tokens = [];
        var blockEnd = start + 1;
        for (; blockEnd < lines.Length; blockEnd++)
        {
            var line = lines[blockEnd];
            var trimmed = line.TrimStart(' ');
            if (trimmed.Length == 0 || (trimmed[0] == '#' && trimmed[1..].Trim().Length == 0))
            {
                continue;
            }

            // A comment inside the metadata is refused, so nothing in the block is left unexamined.
            if (trimmed[0] == '#' && line[0] == ' ')
            {
                return null;
            }

            if (line[0] != ' ')
            {
                break;
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

        return (Encoding.UTF8.GetString(stream.ToArray()), [.. lines.Take(start), .. lines.Skip(blockEnd)]);
    }

    /// <summary>
    /// Returns whether the text has a YAML tag or a <c>%</c> directive anywhere: a <c>!</c> that starts a line or follows a space, tab, <c>[</c>, <c>{</c>, or
    /// <c>,</c>, or a line that starts with <c>%</c>. go-yaml decodes tagged scalars (<c>!!binary c29wcw==</c> is the key <c>sops</c>), so no tag is
    /// accepted: a fully encrypted file has none, and refusing them is cheaper than reading each one. An exclamation mark inside a word is not a tag.
    /// </summary>
    internal static bool HasTag(string yaml)
    {
        foreach (var line in yaml.ReplaceLineEndings("\n").Split('\n'))
        {
            var trimmed = line.TrimStart(' ', '\t');
            if (trimmed.StartsWith('%', StringComparison.Ordinal) || trimmed.StartsWith('!', StringComparison.Ordinal))
            {
                return true;
            }

            for (var index = line.IndexOf('!', StringComparison.Ordinal); index > 0; index = line.IndexOf('!', index + 1, StringComparison.Ordinal))
            {
                if (line[index - 1] is ' ' or '\t' or '[' or '{' or ',')
                {
                    return true;
                }
            }
        }

        return false;
    }

    private enum TopLevel
    {
        Other,
        Sops,
        Invalid,
    }

    /// <summary>Classifies one line at column zero, and refuses a tab indent anywhere.</summary>
    private static TopLevel ClassifyTopLevel(string line)
    {
        if (line.Length == 0)
        {
            return TopLevel.Other;
        }

        if (line[0] == '\t' || line.TrimStart(' ').StartsWith('\t', StringComparison.Ordinal))
        {
            return TopLevel.Invalid;
        }

        if (line[0] is ' ' or '#')
        {
            return TopLevel.Other;
        }

        if (IsMarker(line, "---") || IsMarker(line, "...") || line[0] is '%' or '-' or '?' or '&' or '*' or '!' or '{' or '[' or '|' or '>' or '@' or '`')
        {
            return TopLevel.Invalid;
        }

        var colon = FindMappingColon(line);
        if (colon < 1)
        {
            return TopLevel.Invalid;
        }

        var key = line[..colon].Trim(' ', '\t', '"', '\'');
        var value = line[(colon + 1)..].Trim(' ');
        if (key == "<<" || (value.Length > 0 && value[0] is '&' or '*' or '!' or '{' or '[' && value is not ("[]" or "{}")))
        {
            return TopLevel.Invalid;
        }

        if (!string.Equals(key, "sops", StringComparison.OrdinalIgnoreCase))
        {
            return TopLevel.Other;
        }

        // Anything that reads as the metadata key must be spelled exactly 'sops:' with nothing after it.
        return line.StartsWith("sops:", StringComparison.Ordinal) && value.Length == 0 ? TopLevel.Sops : TopLevel.Invalid;
    }

    private static bool IsMarker(string line, string marker) =>
        line.StartsWith(marker, StringComparison.Ordinal) && (line.Length == 3 || line[3] is ' ' or '\t');

    /// <summary>Finds the colon that ends a mapping key: outside quotes, followed by a space or the end of the line.</summary>
    internal static int FindMappingColon(string text)
    {
        if (text[0] is '"' or '\'')
        {
            var close = text.IndexOf(text[0], 1, StringComparison.Ordinal);
            if (close < 0)
            {
                return -1;
            }

            var after = close + 1;
            while (after < text.Length && text[after] == ' ')
            {
                after++;
            }

            return after < text.Length && text[after] == ':' && (after + 1 == text.Length || text[after + 1] == ' ') ? after : -1;
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
                if (rest.Length > 0 && rest[0] == '#')
                {
                    return false;
                }

                if (rest.Length == 0)
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
            if (rest.Length > 0 && rest[0] == '#')
            {
                return false;
            }

            if (rest.Length == 0)
            {
                if (_position < tokens.Count && (tokens[_position].Indent > indent || (tokens[_position].Indent == indent && IsSequenceItem(tokens[_position].Text))))
                {
                    return ParseBlock(tokens[_position].Indent, depth + 1);
                }

                writer.WriteNullValue();
                return true;
            }

            if (rest[0] == '>' || (rest[0] == '|' && rest is not ("|" or "|-" or "|+")))
            {
                return false;
            }

            if (rest[0] == '|')
            {
                // A literal block scalar holds the armored age file of a recipient; keep its lines.
                List<string> content = [];
                while (_position < tokens.Count && tokens[_position].Indent > indent)
                {
                    content.Add(tokens[_position].Text.TrimEnd());
                    _position++;
                }

                writer.WriteStringValue(string.Join('\n', content));
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

            if (raw.Contains(" #", StringComparison.Ordinal))
            {
                return false;
            }

            var plain = raw.TrimEnd(' ');
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

            var close = text.IndexOf(text[0], 1, StringComparison.Ordinal);
            if (close < 0 || (text[0] == '"' && text.Contains('\\', StringComparison.Ordinal)))
            {
                return null;
            }

            var after = text[(close + 1)..].TrimStart(' ');
            return after.Length == 0 ? text[1..close] : null;
        }
    }
}
