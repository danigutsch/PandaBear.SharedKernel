namespace SharedKernel.Versioning.Tool;

internal static class PublicApiBaselineCommand
{
    public static async Task Run(string repoRoot, TextWriter output)
    {
        PublicApiBaselineChecker.EnsureBaselinesPresent(repoRoot);
        await output.WriteLineAsync("Public API baselines are present for SharedKernel projects.").ConfigureAwait(false);
    }
}
