namespace SharedKernel.Repositories.Tests;

/// <summary>Checks forge-backed priority fallback and queries using local synthetic issue data.</summary>
public sealed class ForgeRoadmapReaderTests
{
    [Theory]
    [InlineData(ForgeIssueCapabilities.Parent, "example/planning#1")]
    [InlineData(ForgeIssueCapabilities.None, "example/planning#2")]
    public async Task Uses_forge_parent_when_supported_and_local_parent_otherwise(ForgeIssueCapabilities capabilities, string expectedParent)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/priorities.json", """
            {
              "example/planning#1": { "order": 2 },
              "example/planning#2": { "order": 1 },
              "example/planning#3": { "order": 3, "parent": "example/planning#2" }
            }
            """);
        var reader = new ForgeRoadmapFixture(capabilities, [
            ForgeRoadmapFixture.Issue(1),
            ForgeRoadmapFixture.Issue(2),
            ForgeRoadmapFixture.Issue(3) with { Parent = new ForgeIssueLink(ForgeRoadmapFixture.Reference(1), true) }
        ]);

        // Act
        var result = await ForgeRoadmapReader.Load(workspace.RootPath, "example/planning", reader, TestContext.Current.CancellationToken);

        // Assert
        result.Issues.ShouldBeEmpty();
        result.OpenItems().Select(item => item.Id).ShouldBe(["example/planning#2", "example/planning#1", "example/planning#3"]);
        result.OpenItems().ShouldHaveSingleItem(item => item.Id == "example/planning#3").Parent.ShouldBe(expectedParent);
    }

    [Fact]
    public async Task Excludes_untriaged_issues_and_resolves_closed_external_blockers()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/priorities.json", """
            {
              "example/planning#1": { "order": 1 },
              "example/planning#2": { "order": 2 }
            }
            """);
        var blocker = new ForgeIssueReference("other/project", 4000000000);
        var reader = new ForgeRoadmapFixture(ForgeIssueCapabilities.Blockers, [
            ForgeRoadmapFixture.Issue(1) with { BlockedBy = [new ForgeIssueLink(ForgeRoadmapFixture.Reference(2), true)] },
            ForgeRoadmapFixture.Issue(2) with { BlockedBy = [new ForgeIssueLink(blocker, false)] },
            ForgeRoadmapFixture.Issue(3) with { Labels = ["untriaged"] }
        ], [new ForgeIssue(blocker, "Closed external blocker", false, null, [], null, [])]);

        // Act
        var result = await ForgeRoadmapReader.Load(workspace.RootPath, "example/planning", reader, TestContext.Current.CancellationToken);

        // Assert
        result.Issues.ShouldBeEmpty();
        result.OpenItems(unblockedOnly: true).Select(item => item.Id).ShouldBe(["example/planning#2"]);
        result.BlockersOf("example/planning#2").ShouldHaveSingleItem().Title.ShouldBe("Closed external blocker");
    }

    [Fact]
    public async Task Reports_unresolved_priority_keys_and_open_issues_without_order()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile("roadmap/priorities.json", """{ "other/project#7": { "order": 1 } }""");
        var reader = new ForgeRoadmapFixture(ForgeIssueCapabilities.Blockers, [ForgeRoadmapFixture.Issue(1)]);

        // Act
        var result = await ForgeRoadmapReader.Load(workspace.RootPath, "example/planning", reader, TestContext.Current.CancellationToken);

        // Assert
        result.Issues.Select(issue => issue.Message).ShouldBe([
            "Issue other/project#7 could not be read.",
            "Open issue example/planning#1 must have an order or the untriaged label."
        ]);
    }
}
