namespace SharedKernel.RepoConfig.Tests;

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
}
