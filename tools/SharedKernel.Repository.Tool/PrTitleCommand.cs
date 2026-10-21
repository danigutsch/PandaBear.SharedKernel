using System.Text.Json;
using System.Text.RegularExpressions;

namespace PandaBear.Repository.Tool;

internal static partial class PrTitleCommand
{
    public static int Run(string[] args)
    {
        if (args is not ["--event", var path])
        {
            throw new ArgumentException("Expected --event <GitHub event JSON>.");
        }
        using var document = JsonDocument.Parse(File.ReadAllText(path));
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
        return 0;
    }

    [GeneratedRegex(@"\A(build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test)(\([^\r\n()]+\))?!?: [^\r\n]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex TitlePattern();
}
