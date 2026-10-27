using System.Globalization;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the node identity that GitHub roadmap import records.</summary>
public sealed class GitHubRoadmapNewIdentityTests
{
    /// <summary>Verifies that the first import stores the GitHub node identity and that a second import changes nothing.</summary>
    [Fact]
    public async Task Initial_import_persists_node_identity_and_repeated_import_is_idempotent()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var initialized = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        initialized.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        GitHubRoadmapReconcileIssue[] snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "New issue", "OPEN") with { NodeId = "I_new" }
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var client = new HttpClient(handler);

        // Act
        var reconciled = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);
        var imported = await RepoConfigToolRunner.Run(["github", "import", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);
        var repeated = await RepoConfigToolRunner.Run(["github", "import", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);
        var item = JsonNode.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        var nodeId = item?["integrations"]?["github"]?["nodeId"]?.GetValue<string>();

        // Assert
        reconciled.ShouldBe(0);
        imported.ShouldBe(0);
        nodeId.ShouldBe("I_new");
        repeated.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
    }
}
