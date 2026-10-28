using System.Security.Cryptography;
using System.Text;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The first line of a managed file: a comment that holds the SHA-256 of the content after it. Markdown files use an
/// HTML comment and every other file a <c>#</c> comment.
/// </summary>
internal static class BaselineMarker
{
    private const string Prefix = "Managed by sharedkernel-repo baseline (sha256 ";
    private const string Suffix = "); change the baseline configuration instead of editing this file.";
    private const int HashLength = 64;

    /// <summary>Returns <paramref name="body"/> preceded by its marker line.</summary>
    public static string Compose(string path, string body) => Line(path, Hash(body)) + "\n" + body;

    /// <summary>Returns the lowercase hexadecimal SHA-256 of the UTF-8 bytes of <paramref name="body"/>.</summary>
    public static string Hash(string body) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

    /// <summary>Splits LF-normalized <paramref name="text"/> into the marker hash and the body after it.</summary>
    /// <returns><see langword="false"/> when the first line is not a marker for <paramref name="path"/>.</returns>
    public static bool TryRead(string path, string text, out string hash, out string body)
    {
        hash = body = string.Empty;
        var lineEnd = text.IndexOf('\n', StringComparison.Ordinal);
        if (lineEnd < 0)
        {
            return false;
        }

        var template = Line(path, new string('0', HashLength));
        var hashStart = template.IndexOf(new string('0', HashLength), StringComparison.Ordinal);
        var line = text[..lineEnd];
        if (line.Length != template.Length
            || !line.StartsWith(template[..hashStart], StringComparison.Ordinal)
            || !line.EndsWith(template[(hashStart + HashLength)..], StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = line.Substring(hashStart, HashLength);
        if (!candidate.All(static c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'))
        {
            return false;
        }

        hash = candidate;
        body = text[(lineEnd + 1)..];
        return true;
    }

    private static string Line(string path, string hash) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? $"<!-- {Prefix}{hash}{Suffix} -->"
            : $"# {Prefix}{hash}{Suffix}";
}
