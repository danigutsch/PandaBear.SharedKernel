using System.Globalization;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Runs <c>verify</c> on a temporary workspace through the command line.</summary>
internal static class RoadmapVerifyTestOperations
{
    /// <summary>Runs <c>verify --root</c> on the workspace and returns the exit code with the captured output and error text.</summary>
    /// <param name="workspace">The workspace to verify.</param>
    public static async Task<(int ExitCode, string Output, string Error)> Verify(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
