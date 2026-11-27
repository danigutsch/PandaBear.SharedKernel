namespace SharedKernel.RepoConfig.Tests;

/// <summary>Runs the read-only forge path through the existing roadmap commands.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class ForgeRoadmapCommandTests
{
    [Fact]
    public async Task Blockers_of_resolves_a_closed_issue_without_a_priority_entry()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/priorities.json", """{ "owner/repository#2": { "order": 1 } }""");
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, [
            GitHubRoadmapSnapshotTestOperations.Issue(1, "Closed work", "CLOSED",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(2, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(2, "Unfinished blocker", "OPEN")
        ]);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var code = await RepoConfigToolRunner.Run(["get", "blockers-of", "owner/repository#1", "--repository", "owner/repository"],
            output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        code.ShouldBe(0, error.ToString());
        output.ToString().ShouldStartWith("owner/repository#2 |", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Next_work_promotes_an_unblocked_issue_that_blocks_open_work()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/priorities.json", """
            { "owner/repository#1": { "order": 1 }, "owner/repository#2": { "order": 2 }, "owner/repository#3": { "order": 3 } }
            """);
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse([
            GitHubRoadmapSnapshotTestOperations.Issue(1, "Independent work", "OPEN"),
            GitHubRoadmapSnapshotTestOperations.Issue(2, "Blocked work", "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(3, "OPEN", "owner/repository")]),
            GitHubRoadmapSnapshotTestOperations.Issue(3, "Unblocking work", "OPEN")
        ]));
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var code = await RepoConfigToolRunner.Run(["get", "next-work", "--repository", "owner/repository"],
            output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        code.ShouldBe(0, error.ToString());
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(2);
        lines[0].ShouldStartWith("owner/repository#3 |", StringComparison.Ordinal);
        lines[1].ShouldStartWith("owner/repository#1 |", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_a_missing_order_without_requiring_item_files()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/priorities.json", "{}");
        using var handler = new TestHttpMessageHandler();
        using var client = new HttpClient(handler);
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse([
            GitHubRoadmapSnapshotTestOperations.Issue(1, "Missing priority", "OPEN")
        ]));
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var code = await RepoConfigToolRunner.Run(["verify", "--repository", "owner/repository"],
            output, error, workspace.RootPath, client, TestContext.Current.CancellationToken);

        // Assert
        code.ShouldBe(1);
        error.ToString().ShouldContain("Open issue owner/repository#1 must have an order", StringComparison.Ordinal);
    }
}
