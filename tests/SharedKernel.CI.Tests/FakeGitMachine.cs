using PandaBear.Processes;

namespace PandaBear.CI.Tests;

/// <summary>Creates a CI machine that finds a fake <c>git</c> and runs it through either git entry point of the machine.</summary>
internal static class FakeGitMachine
{
    /// <summary>Returns a machine with the real process runner that finds <c>git</c> only in <paramref name="gitDirectory"/> and stops it after <paramref name="gitTimeout"/>.</summary>
    public static CiMachine Create(string gitDirectory, TimeSpan gitTimeout) =>
        new(new SystemProcessRunner(), new PathExecutableResolver(gitDirectory, isWindows: false), TimeProvider.System) { GitTimeout = gitTimeout };

    /// <summary>Runs <c>git status</c> in <paramref name="directory"/> through <c>Run</c> when <paramref name="entry"/> is <c>run</c>, otherwise through <c>RunGit</c>.</summary>
    public static Task<string> Run(CiMachine machine, string entry, string directory) => entry == "run"
        ? machine.Run("git", ["status"], directory, TestContext.Current.CancellationToken)
        : machine.RunGit(["status"], directory, TestContext.Current.CancellationToken);
}
