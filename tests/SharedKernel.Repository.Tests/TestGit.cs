using PandaBear.RepoConfig.Tests;

namespace PandaBear.Repository.Tests;

/// <summary>Runs Git for test setup without the user's global or system configuration, hooks, or signing.</summary>
internal static class TestGit
{
    private static readonly Dictionary<string, string?> Environment = new(StringComparer.Ordinal)
    {
        ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
        ["GIT_CONFIG_NOSYSTEM"] = "1",
    };

    /// <summary>Creates a repository in <paramref name="directory"/> that commits every file in it, and returns the commit.</summary>
    public static async Task<string> CommitAll(string directory, CancellationToken ct)
    {
        await Run(directory, ["init", "--quiet"], ct);
        await Run(directory, ["add", "--all"], ct);
        await Run(directory, ["-c", "user.name=Stage Tests", "-c", "user.email=stage-tests@example.invalid", "commit", "--quiet", "--message", "test: commit"], ct);
        return (await Run(directory, ["rev-parse", "HEAD"], ct)).Trim();
    }

    /// <summary>Commits every file in the repository at <paramref name="directory"/> and returns the commit.</summary>
    public static async Task<string> Commit(string directory, CancellationToken ct)
    {
        await Run(directory, ["add", "--all"], ct);
        await Run(directory, ["-c", "user.name=Stage Tests", "-c", "user.email=stage-tests@example.invalid", "commit", "--quiet", "--message", "test: change"], ct);
        return (await Run(directory, ["rev-parse", "HEAD"], ct)).Trim();
    }

    public static async Task<string> Run(string directory, string[] arguments, CancellationToken ct)
    {
        var result = await CiTestSelectionTestProcess.Run("git", directory, arguments, ct, Environment);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + result.StandardError);
    }
}
