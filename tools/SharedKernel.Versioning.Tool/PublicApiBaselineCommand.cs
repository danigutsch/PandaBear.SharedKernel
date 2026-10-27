namespace PandaBear.Versioning.Tool;

/// <summary>Checks that every SharedKernel project has public API baseline files.</summary>
internal static class PublicApiBaselineCommand
{
    /// <summary>Throws when a baseline is missing under <paramref name="repoRoot"/>, otherwise reports that all are present.</summary>
    public static async Task Run(string repoRoot, TextWriter output)
    {
        PublicApiBaselineChecker.EnsureBaselinesPresent(repoRoot);
        await output.WriteLineAsync("Public API baselines are present for SharedKernel projects.").ConfigureAwait(false);
    }
}
