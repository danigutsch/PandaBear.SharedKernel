using System.Text.Json.Nodes;

namespace SharedKernel.Repositories.GitHub.Tests;

/// <summary>Engine tests split from the command-line tests in GitHubRoadmapExternalBlockerTests.</summary>
public sealed class GitHubRoadmapReconciliationTests
{
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
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        RoadmapInitializer.Initialize(workspace.RootPath);
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

    [Fact]
    public async Task GitHub_roadmap_reconciliation_load_rejects_an_impact_cap_below_one()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new InvariantStringWriter();
        using var initError = new InvariantStringWriter();
        RoadmapInitializer.Initialize(workspace.RootPath);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "snapshotDigest": "0000000000000000000000000000000000000000000000000000000000000000",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 0.5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "blockerEdges": [],
              "closedItemTransitions": [],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [],
              "integrity": {
                "expectedIssueCount": 0,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 0,
                "expectedBlockerEdgeCount": 0,
                "dispositionsAreDisjoint": true,
                "dispositionsCoverSnapshot": true
              }
            }
            """);
        Action load = () => GitHubRoadmapReconciliation.Load(workspace.RootPath);

        // Act
        var exception = load.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("GitHub reconciliation manifest contains invalid mechanical priority values.", StringComparison.Ordinal);
    }
}
