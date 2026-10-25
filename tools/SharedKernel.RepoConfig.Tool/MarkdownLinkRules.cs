using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The text rules of <c>verify-links</c>: lines, fenced code, headings and their anchors, link targets, and issue and
/// pull request references. Whitespace, word boundaries, lowercasing, line breaks, percent-decoding, and URL parsing
/// follow Python's <c>str</c>, <c>re</c>, and <c>urllib.parse</c>, so that results match the Python link linter that
/// the command replaces.
/// </summary>
internal static partial class MarkdownLinkRules
{
    /// <summary>
    /// The finding for a direct GitHub issue or pull request URL.
    /// </summary>
    public const string GitHubIssueOrPullRequestUrl =
        "direct GitHub issue/PR link is not allowed; summarize durable context or link a maintained doc instead";

    private const string RepositoryRelativeIssueOrPullRequest =
        "repo-relative issue/PR link is not allowed; summarize durable context or link a maintained doc instead";

    private const string BareIssueOrPullRequestReference =
        "bare issue/PR reference is not allowed; summarize durable context or link a maintained doc instead";

    // Python's whitespace (str.isspace and re's \s) adds the information separators U+001C to U+001F to .NET's.
    private const string Space = @"\s\x1C-\x1F";

    // Python's \w holds letters, numbers, and the underscore, but not the combining marks and connectors of .NET's \w.
    private const string Word = @"\p{L}\p{N}_";
    private const string Boundary = "(?:(?<=[" + Word + "])(?![" + Word + "])|(?<![" + Word + "])(?=[" + Word + "]))";
    private const string TokenTrim = "<>()[]{}\"'`.,;";

    /// <summary>
    /// Splits text into lines as Python's universal newlines and <c>str.splitlines</c> do.
    /// </summary>
    public static List<string> SplitLines(string text)
    {
        List<string> lines = [];
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is '\n' or '\r' or '\v' or '\f' or '\x1C' or '\x1D' or '\x1E' or '\x85' or '\u2028' or '\u2029')
            {
                lines.Add(text[start..index]);
                if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                start = index + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    /// <summary>
    /// Returns the one-based number and content of each line outside fenced code. Fence lines are left out.
    /// </summary>
    public static IEnumerable<(int Number, string Line)> NonFencedLines(IReadOnlyList<string> lines)
    {
        var inFence = false;
        var fenceMarker = string.Empty;
        for (var index = 0; index < lines.Count; index++)
        {
            if (IsFence(lines[index], ref inFence, ref fenceMarker))
            {
                continue;
            }

            if (!inFence)
            {
                yield return (index + 1, lines[index]);
            }
        }
    }

    /// <summary>
    /// Returns the anchors of the headings in <paramref name="text"/>. Every line outside fenced code that starts with
    /// <c>#</c> after leading whitespace is a heading; a repeated slug gets the suffix <c>-1</c>, <c>-2</c>, and so on.
    /// </summary>
    public static HashSet<string> Anchors(string text)
    {
        HashSet<string> anchors = new(StringComparer.Ordinal);
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        var inFence = false;
        var fenceMarker = string.Empty;
        foreach (var line in SplitLines(text))
        {
            var stripped = TrimStartSpace(line);
            if (IsFence(line, ref inFence, ref fenceMarker) || inFence || !stripped.StartsWith('#'))
            {
                continue;
            }

            var heading = TrimSpace(stripped.TrimStart('#'));
            if (heading.Length == 0)
            {
                continue;
            }

            var slug = Slugify(heading);
            var count = counts[slug] = counts.GetValueOrDefault(slug) + 1;
            anchors.Add(count == 1 ? slug : string.Create(CultureInfo.InvariantCulture, $"{slug}-{count - 1}"));
        }

        return anchors;
    }

    /// <summary>
    /// Returns the issue and pull request reference findings of a line outside fenced code.
    /// </summary>
    public static IEnumerable<string> PolicyFindings(string line)
    {
        if (SplitSpace(line).Any(static token => IsGitHubIssueOrPullRequestUrl(token.Trim(TokenTrim.ToCharArray()))))
        {
            yield return GitHubIssueOrPullRequestUrl;
        }

        if (RepositoryRelativeIssueOrPullRequestPattern().IsMatch(line))
        {
            yield return RepositoryRelativeIssueOrPullRequest;
        }

        var withoutExemptions = NonTrackerNumericHashPattern().Replace(
            ColorValuePattern().Replace(ExternalUrlPattern().Replace(line, string.Empty), string.Empty),
            string.Empty);
        if (IssueOrPullRequestIdentifierPattern().IsMatch(withoutExemptions) || BareIssueOrPullRequestPattern().IsMatch(withoutExemptions))
        {
            yield return BareIssueOrPullRequestReference;
        }
    }

    /// <summary>
    /// Returns the link targets of a line outside fenced code: inline links up to the first <c>)</c>, reference
    /// definitions, and autolinks that hold <c>://</c> or start with <c>#</c>. An inline link without a target yields
    /// an empty string.
    /// </summary>
    public static IEnumerable<string> LinkTargets(string line)
    {
        var searchStart = 0;
        while (true)
        {
            var start = line.IndexOf("](", searchStart, StringComparison.Ordinal);
            var end = start < 0 ? -1 : line.IndexOf(')', start + 2);
            if (end < 0)
            {
                break;
            }

            yield return SplitSpace(line[(start + 2)..end]).FirstOrDefault() ?? string.Empty;
            searchStart = end + 1;
        }

        var reference = ReferenceDefinitionPattern().Match(line);
        if (reference.Success)
        {
            yield return reference.Groups[1].Value;
        }

        foreach (Match autolink in AutolinkPattern().Matches(line))
        {
            var target = autolink.Groups[1].Value;
            if (target.Contains("://", StringComparison.Ordinal) || target.StartsWith('#'))
            {
                yield return target;
            }
        }
    }

    /// <summary>
    /// Removes surrounding whitespace and angle brackets from a link target.
    /// </summary>
    public static string NormalizeTarget(string target) => TrimSpace(TrimSpace(target).Trim('<', '>'));

    /// <summary>
    /// Reports whether a target is a URL, <c>mailto:</c>, <c>tel:</c>, or <c>urn:</c> reference.
    /// </summary>
    public static bool IsExternalOrSpecial(string target)
    {
        var lower = Lower(target);
        return lower.Contains("://", StringComparison.Ordinal)
            || lower.StartsWith("mailto:", StringComparison.Ordinal)
            || lower.StartsWith("tel:", StringComparison.Ordinal)
            || lower.StartsWith("urn:", StringComparison.Ordinal);
    }

    /// <summary>
    /// Reports whether <paramref name="value"/> parses, as <c>urllib.parse.urlparse</c> parses it, to an
    /// <c>http</c> or <c>https</c> URL on <c>github.com</c> whose path names an issue or pull request number.
    /// </summary>
    public static bool IsGitHubIssueOrPullRequestUrl(string value)
    {
        var url = TrimLeading(value, static character => character <= ' ').Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        var colon = url.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0
            || !char.IsAsciiLetter(url[0])
            || !url[..colon].All(static character => char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.')
            || !(url[..colon].Equals("http", StringComparison.OrdinalIgnoreCase) || url[..colon].Equals("https", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        url = url[(colon + 1)..];
        if (!url.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var netlocEnd = url.IndexOfAny(['/', '?', '#'], 2);
        netlocEnd = netlocEnd < 0 ? url.Length : netlocEnd;
        if (url[2..netlocEnd] != "github.com")
        {
            return false;
        }

        var path = url[netlocEnd..];
        path = path.Split('#', 2)[0].Split('?', 2)[0];
        var parameters = path.IndexOf(';', Math.Max(path.LastIndexOf('/'), 0));
        path = parameters < 0 ? path : path[..parameters];
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4 && parts[2] is "issues" or "pull" && IsDigits(parts[3]);
    }

    /// <summary>
    /// Decodes percent-escapes as <c>urllib.parse.unquote</c> does: runs of <c>%XX</c> decode as UTF-8, invalid
    /// sequences become U+FFFD, and a <c>%</c> without two hexadecimal digits stays as it is.
    /// </summary>
    public static string Unquote(string value) =>
        value.Contains('%', StringComparison.Ordinal)
            ? PercentEscapesPattern().Replace(value, static match => Encoding.UTF8.GetString(Convert.FromHexString(match.Value.Replace("%", string.Empty, StringComparison.Ordinal))))
            : value;

    /// <summary>
    /// Lowercases as Python's <c>str.lower</c> does, including the two-character lowercase of U+0130.
    /// </summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Anchors are lowercase, as the Python linter lowercases them; they are compared ordinally.")]
    public static string Lower(string value) =>
        value.Replace("\u0130", "i\u0307", StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>
    /// Returns the suffix of the last path component as <c>pathlib</c> does: from its last <c>.</c>, unless that dot
    /// starts or ends the name.
    /// </summary>
    public static string Suffix(string path)
    {
        var name = Path.GetFileName(path);
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[dot..] : string.Empty;
    }

    private static bool IsFence(string line, ref bool inFence, ref string fenceMarker)
    {
        var stripped = TrimStartSpace(line);
        if (!stripped.StartsWith("```", StringComparison.Ordinal) && !stripped.StartsWith("~~~", StringComparison.Ordinal))
        {
            return false;
        }

        var marker = stripped[..3];
        if (!inFence)
        {
            inFence = true;
            fenceMarker = marker;
        }
        else if (marker == fenceMarker)
        {
            inFence = false;
        }

        return true;
    }

    private static string Slugify(string heading)
    {
        var value = MarkupPattern().Replace(HtmlTagPattern().Replace(heading, string.Empty), string.Empty);
        value = NonSlugPattern().Replace(TrimSpace(Lower(value)), string.Empty);
        return value.Replace(' ', '-').Trim('-');
    }

    private static bool IsDigits(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index += char.IsSurrogatePair(value, index) ? 2 : 1)
        {
            if (CharUnicodeInfo.GetDigitValue(value, index) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSpace(char character) => char.IsWhiteSpace(character) || character is >= '\x1C' and <= '\x1F';

    private static string TrimSpace(string value) => TrimTrailing(TrimStartSpace(value), IsSpace);

    private static string TrimStartSpace(string value) => TrimLeading(value, IsSpace);

    private static string TrimLeading(string value, Func<char, bool> trimmed)
    {
        var start = 0;
        while (start < value.Length && trimmed(value[start]))
        {
            start++;
        }

        return value[start..];
    }

    private static string TrimTrailing(string value, Func<char, bool> trimmed)
    {
        var end = value.Length;
        while (end > 0 && trimmed(value[end - 1]))
        {
            end--;
        }

        return value[..end];
    }

    private static IEnumerable<string> SplitSpace(string value)
    {
        var start = -1;
        for (var index = 0; index <= value.Length; index++)
        {
            var space = index == value.Length || IsSpace(value[index]);
            if (space && start >= 0)
            {
                yield return value[start..index];
                start = -1;
            }
            else if (!space && start < 0)
            {
                start = index;
            }
        }
    }

    [GeneratedRegex(@"\A[" + Space + @"]{0,3}\[[^\]]+\]:[" + Space + "]+([^" + Space + "]+)")]
    private static partial Regex ReferenceDefinitionPattern();

    [GeneratedRegex("<([^<>\"]+)>")]
    private static partial Regex AutolinkPattern();

    [GeneratedRegex(@"(?:\A|[" + Space + @"(`])(?:\.{0,2}/)*(?:issues|pull)/\d+" + Boundary)]
    private static partial Regex RepositoryRelativeIssueOrPullRequestPattern();

    [GeneratedRegex("(?:" + Boundary + @"(?:GH|PR)-\d+" + Boundary + "|" + Boundary + @"issue-\d+" + Boundary + "|" + Boundary + @"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+#\d+" + Boundary + ")", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IssueOrPullRequestIdentifierPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])#\d+" + Boundary)]
    private static partial Regex BareIssueOrPullRequestPattern();

    [GeneratedRegex(Boundary + "[a-z][a-z0-9+.-]*://[^" + Space + "<>()`]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalUrlPattern();

    [GeneratedRegex(Boundary + "(?:fill|stroke|color|background(?:-color)?|border(?:-color)?)[" + Space + "]*[:=][" + Space + "]*#[0-9a-f]{3,8}" + Boundary, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ColorValuePattern();

    [GeneratedRegex(Boundary + "(?:Unicode Standard Annex|UAX)[" + Space + @"]+#\d+" + Boundary, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonTrackerNumericHashPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"[`*_~\[\]()]")]
    private static partial Regex MarkupPattern();

    [GeneratedRegex("[^a-z0-9 _.-]")]
    private static partial Regex NonSlugPattern();

    [GeneratedRegex("(?:%[0-9a-fA-F]{2})+")]
    private static partial Regex PercentEscapesPattern();
}
