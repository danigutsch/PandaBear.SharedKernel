using System.Text.RegularExpressions;

namespace PandaBear.Testing;

/// <summary>Finds the command lines that a Markdown document, such as a tool README, documents for one invocation.</summary>
public static partial class ReadmeCommandLines
{
    /// <summary>
    /// Returns the arguments of every trimmed line and every inline code span of <paramref name="markdown"/> that starts with
    /// <paramref name="invocation"/> and a space, in document order: the text after the invocation without a trailing
    /// <c># comment</c>. Leading <c>NAME=value</c> environment assignments are skipped. A word that only begins with the
    /// invocation, such as <c>dotnet repository</c> for <c>dotnet repo</c>, and the invocation without arguments do not match.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <param name="invocation">What starts a command line, such as <c>dotnet repo</c> or <c>dotnet tool run repo --</c>.</param>
    /// <returns>The argument text of each command line, for a command-line parser to split.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="invocation"/> is <see langword="null"/>, empty, or white space.</exception>
    public static string[] Find(string markdown, string invocation)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(invocation);
        var prefix = invocation + " ";
        return
        [
            .. markdown
                .Split('\n')
                .Select(static line => line.Trim())
                .SelectMany(static line => InlineCode().Matches(line).Select(static match => match.Groups["code"].Value).Prepend(line))
                .Select(static text => text[EnvironmentAssignments().Match(text).Length..])
                .Where(text => text.Length > prefix.Length && text.StartsWith(prefix, StringComparison.Ordinal))
                .Select(text => TrailingComment().Replace(text[prefix.Length..], string.Empty)),
        ];
    }

    [GeneratedRegex("`(?<code>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\A(?:[A-Z_]+=\S+\s+)*", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentAssignments();

    [GeneratedRegex(@"\s+#.*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingComment();
}
