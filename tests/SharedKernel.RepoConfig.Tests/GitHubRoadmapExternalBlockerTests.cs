using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests how roadmap reconciliation handles blockers that live in another repository.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapExternalBlockerTests
{
    [Fact]
    public async Task Reconcile_retains_qualified_external_blocker_without_colliding_with_local_number()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
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
}
