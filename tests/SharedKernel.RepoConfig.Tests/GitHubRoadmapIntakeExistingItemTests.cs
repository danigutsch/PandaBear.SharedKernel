using System.Globalization;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapIntakeExistingItemTests
{
    [Fact]
    public async Task Intake_github_rejects_a_reopened_imported_item_without_explicit_review()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var closedItem = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace(
                """
                  "status": "proposed",
                  "order": 11,
                """,
                """
                  "status": "done",
                  "triage": "untriaged",
                """,
                StringComparison.Ordinal)
            .Replace("  \"scoring\": { \"reach\": 1, \"impact\": 1, \"confidence\": 0.1, \"effort\": 3 },\n", string.Empty, StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-018-github-100.json", closedItem);
        workspace.WriteFile(
            "roadmap/order.json",
            """
            {
              "ordering": "lower order values are higher priority",
              "items": ["RM-001"]
            }
            """);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Reopened issue", "OPEN")
        ];
        GitHubRoadmapSnapshotTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "blockerEdges": [],
              "closedItemTransitions": [],
              "directCanonicalPrimaries": [{ "issue": 100, "roadmapItem": "RM-018" }],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [],
              "integrity": {
                "expectedIssueCount": 1,
                "expectedDirectCanonicalPrimaryCount": 1,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 0,
                "expectedBlockerEdgeCount": 0,
                "dispositionsAreDisjoint": true,
                "dispositionsCoverSnapshot": true
              }
            }
            """,
            snapshot);
        var stateBefore = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var stateAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("GitHub open issue #100 maps to closed roadmap item RM-018; review the reopening before intake.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }

    [Fact]
    public async Task Intake_github_refreshes_existing_imported_snapshot_metadata()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        var itemWithStaleStatus = workspace.ReadFile("roadmap/items/RM-018-github-100.json")
            .Replace("\"status\": \"proposed\"", "\"status\": \"ready\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-018-github-100.json", itemWithStaleStatus);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Updated imported issue",
                "OPEN",
                labels: ["type: feature", "priority: high"])
        ];
        GitHubRoadmapSnapshotTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "blockerEdges": [],
              "closedItemTransitions": [],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [100],
              "integrity": {
                "expectedIssueCount": 1,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 1,
                "expectedBlockerEdgeCount": 0,
                "dispositionsAreDisjoint": true,
                "dispositionsCoverSnapshot": true
              }
            }
            """,
            snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        using var item = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        var root = item.RootElement;
        var labels = root.GetProperty("labels").EnumerateArray().Select(label => label.GetString() ?? string.Empty).ToArray();

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        root.GetProperty("title").GetString().ShouldBe("Updated imported issue");
        root.GetProperty("type").GetString().ShouldBe("feature");
        root.GetProperty("status").GetString().ShouldBe("proposed");
        root.GetProperty("scoring").GetProperty("effort").GetDecimal().ShouldBe(5m);
        labels.ShouldBe(["priority: high", "type: feature"]);
    }
}
