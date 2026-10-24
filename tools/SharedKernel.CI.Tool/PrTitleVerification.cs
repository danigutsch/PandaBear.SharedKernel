using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.CI.Tool;

/// <summary>Verifies the pull request title and breaking-change footers in a GitHub event file.</summary>
internal static partial class PrTitleVerification
{
    /// <exception cref="ArgumentException">The title or a breaking-change footer is invalid.</exception>
    public static async Task Verify(string eventPath, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(eventPath, ct).ConfigureAwait(false));
        var title = document.RootElement.GetProperty("pull_request").GetProperty("title").GetString() ?? string.Empty;
        if (title.Length > 100 || title != title.Trim() || !TitlePattern().IsMatch(title))
        {
            throw new ArgumentException("PR title must be a Conventional Commit header of at most 100 characters.");
        }
        var body = document.RootElement.GetProperty("pull_request").TryGetProperty("body", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        foreach (var line in body.Split('\n'))
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
    private static partial Regex TitlePattern();
}
