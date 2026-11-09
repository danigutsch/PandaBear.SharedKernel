using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Creates real Git work trees for tests that run Git itself.</summary>
internal static class GitCheckout
{
    /// <summary>Initializes an empty Git work tree in <paramref name="directory"/>.</summary>
    public static Task Init(string directory, CancellationToken ct) => ProcessRunner.Run("git", ["init", "--quiet"], directory, ct);

    /// <summary>Runs Git and <c>dotnet format</c> for real and answers every other request with <paramref name="fake"/>.</summary>
    public static Func<string, string[], string, CancellationToken, Task<string>> RealGitAndFormat(ProcessLog fake) =>
        (fileName, arguments, directory, ct) => fileName == "git" || arguments is ["format", ..]
            ? ProcessRunner.Run(fileName, arguments, directory, ct)
            : fake.Run(fileName, arguments, directory, ct);

    /// <summary>Runs Git for real and answers every other request with <paramref name="fake"/>.</summary>
    public static Func<string, string[], string, CancellationToken, Task<string>> RealGit(ProcessLog fake) =>
        (fileName, arguments, directory, ct) => fileName == "git"
            ? ProcessRunner.Run(fileName, arguments, directory, ct)
            : fake.Run(fileName, arguments, directory, ct);

    /// <summary>Commits everything in <paramref name="directory"/> without signing, so the commit is the base of a later change.</summary>
    public static async Task CommitAll(string directory, CancellationToken ct)
    {
        await ProcessRunner.Run("git", ["add", "-A"], directory, ct);
        await ProcessRunner.Run("git", ["-c", "user.name=t", "-c", "user.email=t@example.test", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "base"], directory, ct);
    }
}
