using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the GitHub label sync command.</summary>
internal static class GitHubLabelSyncTestOperations
{
    /// <summary>Creates a workspace whose repository configuration holds the given JSON.</summary>
    public static TemporaryRepoConfigWorkspace CreateWorkspace(string configuration)
    {
        var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/sharedkernel-repo.json", configuration);
        return workspace;
    }

    /// <summary>Runs <c>github sync-labels</c> against the workspace with the given handler, and returns its exit code, output, and error.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(
        TemporaryRepoConfigWorkspace workspace,
        TestHttpMessageHandler handler,
        bool force,
        string repository = "owner/repository")
    {
        using var client = new HttpClient(handler, disposeHandler: false);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        List<string> arguments = ["github", "sync-labels", "--repository", repository, "--root", workspace.RootPath];
        if (force)
        {
            arguments.Add("--force");
        }

        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, Path.GetTempPath(), client, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
