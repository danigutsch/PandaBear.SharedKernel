using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.CI.Tool;

/// <summary>
/// Verifies the Conventional Commit header and breaking-change footers of the pull request in a GitHub event file or of a
/// git commit message file. Errors are fixed text, because pull request and commit authors control titles and messages.
/// </summary>
internal static partial class CommitMessageVerification
{
    /// <summary>Line that <c>git commit -v</c> writes above the diff; git drops it and everything below it.</summary>
    private const string ScissorsLine = "# ------------------------ >8 ------------------------";

    private static readonly string[] LineBreaks = ["\r\n", "\n"];

    /// <summary>Verifies the pull request title and body in a GitHub event file.</summary>
    /// <exception cref="ArgumentException">The title or a breaking-change footer is invalid.</exception>
    public static async Task VerifyEvent(string eventPath, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(eventPath, ct).ConfigureAwait(false));
        var pullRequest = document.RootElement.GetProperty("pull_request");
        VerifyHeader(pullRequest.GetProperty("title").GetString() ?? string.Empty, "PR title must be a Conventional Commit header of at most 100 characters.");
        var body = pullRequest.TryGetProperty("body", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        VerifyFooters(body.Split('\n'));
    }

    /// <summary>Verifies a commit message file as git passes it to a <c>commit-msg</c> hook.</summary>
    /// <exception cref="ArgumentException">The message has no header, or the header or a breaking-change footer is invalid.</exception>
    public static async Task VerifyFile(string messagePath, CancellationToken ct)
    {
        // Git commits a byte order mark as part of the header, so decoding keeps it and the header check rejects it.
        var message = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(messagePath, ct).ConfigureAwait(false));
        var lines = message.Split(LineBreaks, StringSplitOptions.None)
            .Select(static line => line.TrimEnd())
            .TakeWhile(static line => line != ScissorsLine)
            .Where(static line => !line.TrimStart().StartsWith('#'))
            .SkipWhile(static line => line.Length == 0)
            .ToArray();
        if (lines.Length == 0)
        {
            throw new ArgumentException("Commit message has no header.");
        }

        // Git writes these headers for merge and revert commits.
        if (lines[0].StartsWith("Merge ", StringComparison.Ordinal) || lines[0].StartsWith("Revert ", StringComparison.Ordinal))
        {
            return;
        }

        VerifyHeader(lines[0], "Commit message must start with a Conventional Commit header of at most 100 characters.");
        VerifyFooters(lines.Skip(1));
    }

    private static void VerifyHeader(string header, string error)
    {
        if (header.Length > 100 || header != header.Trim() || !HeaderPattern().IsMatch(header))
        {
            throw new ArgumentException(error);
        }
    }

    private static void VerifyFooters(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var footer = line.StartsWith("BREAKING CHANGE", StringComparison.Ordinal) ? "BREAKING CHANGE: "
                : line.StartsWith("BREAKING-CHANGE", StringComparison.Ordinal) ? "BREAKING-CHANGE: " : null;
            if (footer is not null && (!line.StartsWith(footer, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(line[footer.Length..])))
            {
                throw new ArgumentException("A breaking-change footer requires 'BREAKING CHANGE: <description>'.");
            }
        }
    }

    [GeneratedRegex(@"\A(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\([^\r\n()]+\))?!?: [^\r\n]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderPattern();
}
