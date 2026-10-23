namespace PandaBear.Versioning.Tool;

internal static class BreakingChangeMarkerCommand
{
    /// <summary>Returns whether a commit in <paramref name="range" /> has a breaking-change marker.</summary>
    public static async Task<bool> Run(string range, string repoRoot, TextWriter output, CancellationToken ct)
    {
        var log = await CommandRunner.Run("git", ["log", "--format=%B%x00", "--end-of-options", range], repoRoot, ct).ConfigureAwait(false);
        if (!BreakingChangeMarker.HasMarker(log))
        {
            return false;
        }

        await output.WriteLineAsync("Breaking-change marker found.").ConfigureAwait(false);
        return true;
    }
}
