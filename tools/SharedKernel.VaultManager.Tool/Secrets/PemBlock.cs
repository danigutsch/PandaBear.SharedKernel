namespace PandaBear.VaultManager.Tool.Secrets;

/// <summary>Parsed PEM-style block: label, RFC 1421 headers, and base64 body.</summary>
/// <param name="Label">Label between <c>BEGIN</c> and the dashes, for example <c>RSA PRIVATE KEY</c>.</param>
/// <param name="Headers">Header lines such as <c>Proc-Type: 4,ENCRYPTED</c>.</param>
/// <param name="Body">Base64 body without whitespace.</param>
internal sealed record PemBlock(string Label, IReadOnlyList<string> Headers, string Body)
{
    private const string Dashes = "-----";

    /// <summary>Parses the first PEM block in text.</summary>
    /// <param name="text">PEM text.</param>
    /// <param name="block">Parsed block.</param>
    /// <returns><see langword="true"/> when a complete block was found.</returns>
    public static bool TryParse(string text, out PemBlock? block)
    {
        return TryParse(text, out block, out _);
    }

    /// <summary>Parses the first PEM block in text and reports where it sits.</summary>
    /// <param name="text">PEM text.</param>
    /// <param name="block">Parsed block.</param>
    /// <param name="extent">
    /// Characters of <paramref name="text"/> from the first dash of the <c>BEGIN</c> line through the last dash of the
    /// matching <c>END</c> line.
    /// </param>
    /// <returns><see langword="true"/> when a complete block was found.</returns>
    public static bool TryParse(string text, out PemBlock? block, out Range extent)
    {
        ArgumentNullException.ThrowIfNull(text);
        block = null;
        extent = default;
        int begin = text.IndexOf(Dashes + "BEGIN ", StringComparison.Ordinal);
        if (begin < 0)
        {
            return false;
        }

        int labelStart = begin + Dashes.Length + 6;
        int labelEnd = text.IndexOf(Dashes, labelStart, StringComparison.Ordinal);
        if (labelEnd < 0)
        {
            return false;
        }

        string label = text[labelStart..labelEnd];
        string footer = Dashes + "END " + label + Dashes;
        int end = text.IndexOf(footer, labelEnd, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var headers = new List<string>();
        var body = new System.Text.StringBuilder();
        foreach (var (_, line) in SecretText.Lines(text[(labelEnd + Dashes.Length)..end]))
        {
            string trimmed = line.Trim();
            if (trimmed.Contains(':', StringComparison.Ordinal))
            {
                headers.Add(trimmed);
            }
            else
            {
                body.Append(trimmed);
            }
        }

        block = new PemBlock(label, headers, body.ToString());
        extent = begin..(end + footer.Length);
        return true;
    }

    /// <summary>Whether a legacy <c>Proc-Type: 4,ENCRYPTED</c> header is present.</summary>
    public bool HasEncryptedHeader => Headers.Any(header =>
        header.StartsWith("Proc-Type:", StringComparison.OrdinalIgnoreCase) && header.Contains("ENCRYPTED", StringComparison.OrdinalIgnoreCase));
}
