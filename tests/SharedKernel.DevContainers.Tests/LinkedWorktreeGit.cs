using SharedKernel.Processes;

namespace SharedKernel.DevContainers.Tests;

/// <summary>Creates a linked worktree and looks up its common Git directory with a fake <c>git</c> in the worktree.</summary>
internal static class LinkedWorktreeGit
{
    /// <summary>Returns a temporary directory whose <c>.git</c> file marks it as a linked worktree.</summary>
    public static TemporaryDirectory CreateWorktree()
    {
        var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, ".git"), "gitdir: /repository/.git/worktrees/linked\n");
        return directory;
    }

    /// <summary>Finds the common directory of <paramref name="root"/> with the <c>git</c> in <paramref name="root"/>, stopped after <paramref name="timeout"/>.</summary>
    public static Task<string?> Find(string root, TimeSpan timeout) =>
        DevcontainerVerification.FindGitCommonDirectory(root, new PathExecutableResolver(root, isWindows: false), timeout, TestContext.Current.CancellationToken);
}
