using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

internal static class GitHubLabelSyncTestOperations
{
    public static TemporaryRepoConfigWorkspace CreateWorkspace(string configuration)
    {
        var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/sharedkernel-repo.json", configuration);
        return workspace;
    }

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
