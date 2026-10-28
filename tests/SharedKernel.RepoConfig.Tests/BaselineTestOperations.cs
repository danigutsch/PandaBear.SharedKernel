using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Test helpers for the <c>baseline</c> commands.</summary>
internal static class BaselineTestOperations
{
    /// <summary>A baseline that manages one profile file, one profile file under a nested path, and one sourced file.</summary>
    public const string Configuration = """
        {
          "version": 1,
          "baseline": {
            "repositoryName": "example-repo",
            "defaultBranch": "trunk",
            "files": [
              { "path": ".gitattributes", "profile": "gitattributes" },
              { "path": "docs/README.md", "profile": "readme" },
              { "path": "CODEOWNERS", "source": ".config/baseline/CODEOWNERS" }
            ]
          }
        }
        """;

    /// <summary>Creates a workspace that holds <paramref name="configuration"/> and the sourced CODEOWNERS template.</summary>
    public static TemporaryRepoConfigWorkspace CreateWorkspace(string configuration = Configuration)
    {
        var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/sharedkernel-repo.json", configuration);
        workspace.WriteFile(".config/baseline/CODEOWNERS", "* @example-owners\n# default branch: @@defaultBranch@@\n");
        return workspace;
    }

    /// <summary>Runs <c>baseline &lt;verb&gt;</c> against the workspace and returns the exit code, output, and error.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(
        TemporaryRepoConfigWorkspace workspace,
        string verb,
        bool force = false)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        List<string> arguments = ["baseline", verb, "--root", workspace.RootPath];
        if (force)
        {
            arguments.Add("--force");
        }

        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
