using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapExternalBlockerTests
{
    [Fact]
    public async Task Reconcile_retains_qualified_external_blocker_without_colliding_with_local_number()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var init = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        init.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Local work", "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN", "other/library")])
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var client = new HttpClient(handler);

        // Act
        var result = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        error.ToString().ShouldBe(string.Empty);
        result.ShouldBe(0);
        var path = Directory.GetFiles(Path.Combine(workspace.RootPath, "roadmap", "reconciliation"), "open-issues-*.json").ShouldHaveSingleItem();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        var edges = manifest.RootElement.GetProperty("externalBlockerEdges");
        edges.GetArrayLength().ShouldBe(1);
        edges[0].GetProperty("blocker").GetProperty("repository").GetString().ShouldBe("other/library");
        edges[0].GetProperty("blocker").GetProperty("issue").GetInt32().ShouldBe(100);
        edges[0].GetProperty("blocked").GetProperty("repository").GetString().ShouldBe("owner/repository");
        manifest.RootElement.GetProperty("blockerEdges").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void External_blocker_derivation_rejects_conflicting_endpoint_states()
    {
        // Arrange
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "First", "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(7, "OPEN", "other/library")]),
            GitHubRoadmapSnapshotTestOperations.Issue(101, "Second", "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(7, "CLOSED", "OTHER/library")])
        ];
        Action action = () => GitHubRoadmapReconciler.DeriveExternalBlockerEdges(snapshot, "owner/repository");

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("endpoint states conflict", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("other/library", "CLOSED")]
    [InlineData("different/library", "OPEN")]
    public void Snapshot_digest_binds_external_blocker_repository_and_state(string repository, string state)
    {
        // Arrange
        var original = GitHubRoadmapSnapshotTestOperations.Issue(100, "Local", "OPEN",
            blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(7, "OPEN", "other/library")]);
        var changed = GitHubRoadmapSnapshotTestOperations.Issue(100, "Local", "OPEN",
            blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(7, state, repository)]);

        // Act
        var originalDigest = GitHubIssueSnapshotDigest.Compute([original]);
        var changedDigest = GitHubIssueSnapshotDigest.Compute([changed]);

        // Assert
        changedDigest.ShouldNotBe(originalDigest);
    }

    [Theory]
    [InlineData("same-repository", "exactly one local endpoint")]
    [InlineData("both-external", "exactly one local endpoint")]
    [InlineData("duplicate", "must be unique")]
    [InlineData("conflicting-state", "states conflict")]
    [InlineData("invalid-repository", "repository is invalid")]
    [InlineData("invalid-count", "integrity metadata")]
    public async Task External_blocker_manifest_rejects_ambiguous_or_inconsistent_metadata(string scenario, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var init = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        init.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        var manifestPath = Path.Combine(workspace.RootPath, "roadmap", "reconciliation", "open-issues-test.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken))!.AsObject();
        var edge = JsonNode.Parse("""
            {"blocker":{"repository":"other/library","issue":7,"state":"OPEN"},"blocked":{"repository":"owner/repository","issue":100,"state":"OPEN"}}
            """)!.AsObject();
        var edges = new JsonArray(edge);
        root["externalBlockerEdges"] = edges;
        root["needsHuman"] = new JsonArray(100);
        root["integrity"]!["expectedIssueCount"] = 1;
        root["integrity"]!["expectedNeedsHumanCount"] = 1;
        root["integrity"]!["expectedExternalBlockerEdgeCount"] = 1;
        switch (scenario)
        {
            case "same-repository":
                edge["blocker"]!["repository"] = "owner/repository";
                break;
            case "both-external":
                edge["blocked"]!["repository"] = "another/library";
                break;
            case "duplicate":
                edges.Add(edge.DeepClone());
                break;
            case "conflicting-state":
                var conflicting = edge.DeepClone();
                conflicting["blocker"]!["state"] = "CLOSED";
                conflicting["blocked"]!["issue"] = 101;
                edges.Add(conflicting);
                break;
            case "invalid-repository":
                edge["blocker"]!["repository"] = "https://example.com/owner/repo";
                break;
            case "invalid-count":
                root["integrity"]!["expectedExternalBlockerEdgeCount"] = 2;
                break;
        }

        Action action = () => GitHubRoadmapReconciliation.Parse(manifestPath, root.ToJsonString());

        // Act
        var exception = action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain(expectedError, StringComparison.Ordinal);
    }
}
