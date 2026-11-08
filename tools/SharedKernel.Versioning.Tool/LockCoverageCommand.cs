namespace PandaBear.Versioning.Tool;

/// <summary>Checks that the packages of lock-free source projects are covered by a committed lock file.</summary>
internal static class LockCoverageCommand
{
    /// <summary>Throws when a lock-free project under <paramref name="repoRoot"/> has no restored assets or resolves an uncovered package, otherwise reports how many projects were checked.</summary>
    public static async Task Run(string repoRoot, TextWriter output)
    {
        var checkedProjects = PackageLockInventory.VerifyLockCoverage(repoRoot);
        await output.WriteLineAsync(
            $"Lock coverage verified: {checkedProjects} lock-free project(s) resolve only packages that a committed lock file resolves.").ConfigureAwait(false);
    }
}
