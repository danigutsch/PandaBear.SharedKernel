using System.Globalization;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapIntakeOrderingTests
{
    [Fact]
    public async Task Intake_github_rejects_order_exhaustion()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        var reviewedItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json")
            .Replace("\"order\": 10", $"\"order\": {int.MaxValue.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", reviewedItem);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Order overflow", "OPEN")
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

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("Roadmap order values are exhausted.", StringComparison.Ordinal);
        workspace.FileExists("roadmap/items/RM-018-github-100.json").ShouldBe(false);
    }

    [Fact]
    public async Task Intake_github_places_a_new_blocker_before_an_existing_imported_item()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(
            ["init", "--root", workspace.RootPath],
            initOutput,
            initError,
            workspace.RootPath,
            TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Existing imported item",
                "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                101,
                "New blocker",
                "OPEN",
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN")])
        ];
        GitHubRoadmapSnapshotTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 40,
                "reach": 2,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.5,
                "effort": { "type: epic": 9, "type: feature": 6, "type: enabler": 4, "type: docs": 2, "type: chore": 1, "default": 4 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "blockerEdges": [
                { "blocker": 101, "blockerState": "OPEN", "blocked": 100, "blockedState": "OPEN" }
              ],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [100, 101],
              "integrity": {
                "expectedIssueCount": 2,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 2,
                "expectedBlockerEdgeCount": 1,
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
        var exitCode = await RepoConfigToolApplication.Run(
            ["intake", "github", "--apply", "--root", workspace.RootPath],
            output,
            error,
            workspace.RootPath,
            httpClient,
            TestContext.Current.CancellationToken);
        using var blockerDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-040-github-101.json"));
        using var blockedDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        using var orderDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/order.json"));
        var blocker = blockerDocument.RootElement;
        var blocked = blockedDocument.RootElement;
        var orderedItems = orderDocument.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        blocker.GetProperty("order").GetInt32().ShouldBe(11);
        blocker.GetProperty("blocks").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ShouldBe(["RM-018"]);
        blocked.GetProperty("order").GetInt32().ShouldBe(12);
        blocked.GetProperty("scoring").GetProperty("reach").GetDecimal().ShouldBe(2m);
        blocked.GetProperty("scoring").GetProperty("impact").GetDecimal().ShouldBe(2m);
        blocked.GetProperty("scoring").GetProperty("confidence").GetDecimal().ShouldBe(0.5m);
        blocked.GetProperty("scoring").GetProperty("effort").GetDecimal().ShouldBe(4m);
        blocked.GetProperty("blockedBy").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ShouldBe(["RM-040"]);
        orderedItems.ShouldBe(["RM-001", "RM-040", "RM-018"]);
    }
}
