namespace SharedKernel.Versioning.Tool;

internal static class BreakingChangeMarkerCommand
{
    public static async Task Run(string range, string repoRoot, TextWriter output)
    {
        var log = CommandRunner.Run("git", ["log", "--format=%B%x00", range], repoRoot);
        if (!HasMarker(log))
        {
            throw new ArgumentException($"No breaking-change marker found in {range}.");
        }

        await output.WriteLineAsync("Breaking-change marker found.").ConfigureAwait(false);
    }

    public static bool HasMarker(string messages)
    {
        var commits = messages.Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return commits.Any(static message => message.Contains("\nBREAKING CHANGE:", StringComparison.Ordinal)
            || (ConventionalCommitParser.TryParse(message, out var commit) && commit is not null && commit.Impact == ReleaseImpact.Major));
    }
}
