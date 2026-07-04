namespace SharedKernel.Versioning;

/// <summary>
/// Parses Conventional Commit messages used for release-impact decisions.
/// </summary>
public static class ConventionalCommitParser
{
    /// <summary>
    /// Parses a Conventional Commit message.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <returns>The parsed commit.</returns>
    /// <exception cref="ArgumentException">Thrown when the message is not a valid Conventional Commit.</exception>
    public static ConventionalCommit Parse(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var normalized = message.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        var lines = normalized.Split('\n');
        var header = ParseHeader(lines[0]);
        var remainingLines = lines.Skip(1).ToArray();
        var footers = ParseFooters(remainingLines);
        var body = ParseBody(remainingLines, footers.Count);

        return new ConventionalCommit(
            header.Type,
            header.Scope,
            header.IsBreaking,
            header.Description,
            body,
            footers);
    }

    /// <summary>
    /// Attempts to parse a Conventional Commit message.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="commit">The parsed commit when parsing succeeds.</param>
    /// <returns><see langword="true" /> when parsing succeeds; otherwise, <see langword="false" />.</returns>
    public static bool TryParse(string message, out ConventionalCommit? commit)
    {
        try
        {
            commit = Parse(message);
            return true;
        }
        catch (ArgumentException)
        {
            commit = null;
            return false;
        }
    }

    private static CommitHeader ParseHeader(string header)
    {
        var separator = header.IndexOf(": ", StringComparison.Ordinal);
        if (separator <= 0 || separator == header.Length - 2)
        {
            throw new ArgumentException("The commit header must use '<type>[optional scope][optional !]: <description>'.", nameof(header));
        }

        var prefix = header[..separator];
        var description = header[(separator + 2)..];
        var isBreaking = prefix.EndsWith('!');

        if (isBreaking)
        {
            prefix = prefix[..^1];
        }

        string type;
        string? scope = null;
        var scopeStart = prefix.IndexOf('(', StringComparison.Ordinal);
        if (scopeStart >= 0)
        {
            if (!prefix.EndsWith(')') || scopeStart == 0 || scopeStart == prefix.Length - 2)
            {
                throw new ArgumentException("The commit scope must use '<type>(<scope>)'.", nameof(header));
            }

            type = prefix[..scopeStart];
            scope = prefix[(scopeStart + 1)..^1];
        }
        else
        {
            type = prefix;
        }

        if (!IsToken(type) || scope is not null && !IsToken(scope))
        {
            throw new ArgumentException("The commit type and scope must be non-empty tokens.", nameof(header));
        }

        return new CommitHeader(type, scope, isBreaking, description);
    }

    private static List<string> ParseFooters(string[] lines)
    {
        if (lines.Length == 0)
        {
            return [];
        }

        var footers = new List<string>();
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            if (!IsFooter(line))
            {
                footers.Clear();
                break;
            }

            footers.Insert(0, line);
        }

        return footers;
    }

    private static string? ParseBody(string[] lines, int footerCount)
    {
        if (lines.Length == 0)
        {
            return null;
        }

        var bodyLines = footerCount == 0 ? lines : lines[..^footerCount];
        var body = string.Join('\n', bodyLines).Trim();
        return body.Length == 0 ? null : body;
    }

    private static bool IsFooter(string line)
    {
        var separator = line.IndexOf(": ", StringComparison.Ordinal);
        if (separator <= 0)
        {
            return false;
        }

        var token = line[..separator];
        return token == "BREAKING CHANGE" || IsToken(token);
    }

    private static bool IsToken(string value) => value.All(character => char.IsLetterOrDigit(character) || character == '-');

    private sealed record CommitHeader(string Type, string? Scope, bool IsBreaking, string Description);
}
