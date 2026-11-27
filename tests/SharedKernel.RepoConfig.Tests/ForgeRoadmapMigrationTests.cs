using System.Net;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Runs read-only roadmap migration previews through the command line.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ForgeRoadmapMigrationTests
{
    [Fact]
    public async Task Preview_exports_priority_inputs_without_writing_the_roadmap()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapInitializer.Initialize(workspace.RootPath);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse([
            GitHubRoadmapSnapshotTestOperations.Issue(997, "Mapped work", "OPEN")
        ]));
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var code = await RepoConfigToolRunner.Run(["migrate-roadmap", "--repository", "owner/repository"],
            output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        code.ShouldBe(0, error.ToString());
        using var priorities = JsonDocument.Parse(output.ToString());
        var priority = priorities.RootElement.GetProperty("owner/repository#997");
        priority.GetProperty("order").GetInt32().ShouldBe(10);
        priority.GetProperty("scoring").GetProperty("reach").GetDecimal().ShouldBe(20m);
        priority.TryGetProperty("parent", out _).ShouldBeFalse();
        workspace.FileExists("roadmap/priorities.json").ShouldBeFalse();
    }

    [Fact]
    public async Task Preview_reports_relationship_and_query_drift()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapInitializer.Initialize(workspace.RootPath);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse([
            GitHubRoadmapSnapshotTestOperations.Issue(997, "Mapped work", "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(998, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(998, "Untriaged blocker", "OPEN", labels: ["untriaged"])
        ]));
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var code = await RepoConfigToolRunner.Run(["migrate-roadmap", "--repository", "owner/repository"],
            output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        code.ShouldBe(1);
        error.ToString().ShouldContain("Forge-only blocker owner/repository#998", StringComparison.Ordinal);
        error.ToString().ShouldContain("next-unblocked differs at position 1", StringComparison.Ordinal);
        output.ToString().ShouldContain("owner/repository#997", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_reports_unavailable_issues_without_claiming_link_removal()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapInitializer.Initialize(workspace.RootPath);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        handler.EnqueueJson(HttpStatusCode.Forbidden, "{}");
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var code = await RepoConfigToolRunner.Run(["migrate-roadmap", "--repository", "owner/repository"],
            output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        code.ShouldBe(1);
        error.ToString().ShouldContain("Open issues could not be read", StringComparison.Ordinal);
        error.ToString().ShouldNotContain("local-only", StringComparison.OrdinalIgnoreCase);
        workspace.FileExists("roadmap/priorities.json").ShouldBeFalse();
    }
}
