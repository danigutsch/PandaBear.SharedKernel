using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the Forgejo label sync command.</summary>
internal static class ForgejoLabelSyncTestOperations
{
    /// <summary>Runs <c>github sync-labels --forgejo</c> against the workspace with the given handler, and returns its exit code, output, and error.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunForgejo(
        TemporaryRepoConfigWorkspace workspace,
        TestHttpMessageHandler handler,
        bool force,
        string server = "https://codeberg.example")
    {
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        List<string> arguments = ["github", "sync-labels", "--repository", "owner/repository", "--forgejo", server, "--root", workspace.RootPath];
        if (force)
        {
            arguments.Add("--force");
        }

        var environment = new RepoConfigToolEnvironment(Path.GetTempPath()) { ForgejoHandler = handler };
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, environment, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Returns a label list page with the <c>X-Total-Count</c> header and no <c>Link</c> header, as Forgejo 17 answers.</summary>
    public static HttpResponseMessage CountedLabels(string json, int total)
    {
        var response = HttpResponseFactory.Json(json);
        response.Headers.TryAddWithoutValidation("X-Total-Count", total.ToString(CultureInfo.InvariantCulture));
        return response;
    }
}
