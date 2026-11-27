namespace SharedKernel.RepoConfig.Tests;

/// <summary>Runs GitHub environment protection commands against a fixture HTTP transport.</summary>
internal static class GitHubEnvironmentProtectionTestOperations
{
    /// <summary>Runs the command and captures both output streams.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> Run(
        HttpMessageHandler handler,
        string repository = "owner/repository",
        string environment = "roadmap-maintenance")
    {
        using var client = new HttpClient(handler, disposeHandler: false);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var exitCode = await RepoConfigToolRunner.Run(
            ["github", "verify-environment-protection", "--repository", repository, "--environment", environment],
            output, error, Path.GetTempPath(), client, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
