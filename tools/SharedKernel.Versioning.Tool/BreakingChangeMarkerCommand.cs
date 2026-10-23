namespace PandaBear.Versioning.Tool;

internal static class BreakingChangeMarkerCommand
{
    public static async Task Run(string range, string repoRoot, TextWriter output, CancellationToken ct)
    {
        var log = await CommandRunner.Run("git", ["log", "--format=%B%x00", range], repoRoot, ct).ConfigureAwait(false);
        if (!BreakingChangeMarker.HasMarker(log))
        {
            throw new ArgumentException($"No breaking-change marker found in {range}.");
        }

        await output.WriteLineAsync("Breaking-change marker found.").ConfigureAwait(false);
    }
}
