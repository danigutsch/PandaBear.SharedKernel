using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

internal static class RoadmapConfigTestOperations
{
    public static void EnableGitHubSync(TemporaryRepoConfigWorkspace workspace, string repository = "owner/repository")
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);

        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace(
            "\"enabled\": false",
            $"\"enabled\": true,\n      \"repository\": \"{repository}\"",
            StringComparison.Ordinal));
    }

    public static void SetProperty(TemporaryRepoConfigWorkspace workspace, string name, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var config = JsonNode.Parse(workspace.ReadFile("roadmap/config.json"))!.AsObject();
        config.Remove(name);
        if (value is not null)
        {
            config[name] = value;
        }

        workspace.WriteFile("roadmap/config.json", config.ToJsonString());
    }

    public static async Task<(int ExitCode, string Output, string Error)> Verify(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        return (exitCode, output.ToString(), error.ToString());
    }
}
