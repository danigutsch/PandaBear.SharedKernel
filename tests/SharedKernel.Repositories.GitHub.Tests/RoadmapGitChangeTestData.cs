namespace PandaBear.Repositories.GitHub.Tests;

/// <summary>Captured roadmap changes for publication tests.</summary>
internal static class RoadmapGitChangeTestData
{
    /// <summary>Writes <c>roadmap/items/RM-100.json</c> in the workspace and captures it as the only changed file.</summary>
    /// <param name="workspace">The workspace that receives the file.</param>
    public static RoadmapGitChangeSet CreateChanges(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        workspace.WriteFile("roadmap/items/RM-100.json", "{}\n");
        return RoadmapGitChangeGuard.Capture(
            workspace.RootPath,
            RoadmapGitStatusTestData.Ordinary("roadmap/items/RM-100.json"));
    }
}
