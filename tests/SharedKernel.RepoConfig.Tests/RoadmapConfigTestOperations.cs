using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Edits and verifies the <c>roadmap/config.json</c> file of a temporary workspace.</summary>
internal static class RoadmapConfigTestOperations
{
    /// <summary>Switches the config's GitHub sync from disabled to enabled and sets its repository.</summary>
    /// <param name="workspace">The workspace whose config is rewritten.</param>
    /// <param name="repository">The <c>owner/name</c> repository written into the config.</param>
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

    /// <summary>Replaces one top-level property of the config JSON, or removes it when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="workspace">The workspace whose config is rewritten.</param>
    /// <param name="name">The top-level property name.</param>
    /// <param name="value">The new value, or <see langword="null"/> to remove the property.</param>
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
