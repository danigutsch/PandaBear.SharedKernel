using System.Text;

namespace SharedKernel.Secrets;

/// <summary>
/// Reads the header of the armored age file that SOPS stores for each recipient (<c>enc</c>) and checks its structure against the recipient label.
/// </summary>
/// <remarks>
/// This proves the label and the structure, not who can decrypt: an X25519 stanza carries an ephemeral share and a wrapped key, never the
/// recipient, so only a trial decryption by each holder shows that a label is honest. What is checked: the armor, one stanza and one only,
/// the stanza type the label requires, and for SSH recipients the key tag that age derives from the public key.
/// </remarks>
internal static class AgeEnvelope
{
    /// <summary>The longest <c>enc</c> text that is read.</summary>
    internal const int MaxLength = 64 * 1024;

    private const string Begin = "-----BEGIN AGE ENCRYPTED FILE-----";
    private const string End = "-----END AGE ENCRYPTED FILE-----";
    private const string Magic = "age-encryption.org/v1";
    private const int ShareLength = 32;

    /// <summary>Returns whether <paramref name="armored"/> is an age file with exactly one recipient stanza that suits <paramref name="recipient"/>.</summary>
    internal static bool IsSingleStanzaFor(AgeRecipient recipient, string armored)
    {
        if (armored.Length > MaxLength || !TryReadHeaderLines(armored, out var lines))
        {
            return false;
        }

        List<string[]> stanzas = [];
        foreach (var line in lines)
        {
            if (line.StartsWith("-> ", StringComparison.Ordinal))
            {
                var fields = line[3..].Split(' ');
                if (fields.Any(static f => f.Length == 0))
                {
                    return false;
                }

                stanzas.Add(fields);
            }
        }

        if (stanzas.Count != 1)
        {
            return false;
        }

        var stanza = stanzas[0];
        var arguments = stanza[1..];
        return recipient.Kind switch
        {
            AgeRecipientKind.X25519 => stanza[0] == "X25519" && arguments.Length == 1 && IsShare(arguments[0]),
            AgeRecipientKind.Ssh when recipient.Value.StartsWith("ssh-ed25519 ", StringComparison.Ordinal) =>
                stanza[0] == "ssh-ed25519" && arguments.Length == 2 && arguments[0] == recipient.SshTag() && IsShare(arguments[1]),
            AgeRecipientKind.Ssh => stanza[0] == "ssh-rsa" && arguments.Length == 1 && arguments[0] == recipient.SshTag(),
            _ => true,
        };
    }

    private static bool TryReadHeaderLines(string armored, out List<string> headerLines)
    {
        headerLines = [];
        var lines = armored.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        if (lines.Length < 3 || lines[0] != Begin || lines[^1] != End)
        {
            return false;
        }

        var body = string.Concat(lines[1..^1]);
        if (lines[1..^1].Any(static l => l.Length is 0 or > 64 || !l.All(static c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '=')))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(body);
        }
        catch (FormatException)
        {
            return false;
        }

        var position = 0;
        var first = true;
        while (position < bytes.Length)
        {
            var end = Array.IndexOf(bytes, (byte)'\n', position);
            if (end < 0)
            {
                return false;
            }

            var raw = bytes.AsSpan(position, end - position);
            foreach (var b in raw)
            {
                if (b is < 0x20 or > 0x7e)
                {
                    return false;
                }
            }

            var line = Encoding.ASCII.GetString(raw);
            position = end + 1;
            if (first)
            {
                if (line != Magic)
                {
                    return false;
                }

                first = false;
                continue;
            }

            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                return true;
            }

            headerLines.Add(line);
        }

        return false;
    }

    private static bool IsShare(string text)
    {
        if (text.Length != 43 || !text.All(static c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/'))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(text + "=").Length == ShareLength;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
