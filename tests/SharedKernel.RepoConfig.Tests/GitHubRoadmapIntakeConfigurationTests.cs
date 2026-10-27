using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the configuration that GitHub roadmap import and reconciliation require.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapIntakeConfigurationTests
{
    /// <summary>Verifies that verification rejects an item identifier prefix that is unsafe for file names.</summary>
    [Theory]
    [InlineData("../outside")]
    [InlineData("/tmp/outside")]
    [InlineData("RM/child")]
    public async Task Verify_rejects_an_unsafe_item_id_prefix(string itemIdPrefix)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var config = workspace.ReadFile("roadmap/config.json")
            .Replace("\"itemIdPrefix\": \"RM\"", $"\"itemIdPrefix\": \"{itemIdPrefix}\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/config.json", config);

        // Act
        var issues = RepoConfigVerifier.Verify(workspace.RootPath);
        var messages = string.Join('\n', issues.Select(issue => issue.Message));

        // Assert
        messages.ShouldContain("itemIdPrefix may contain only ASCII letters, digits, underscores, and hyphens.", StringComparison.Ordinal);
    }

    /// <summary>Verifies that loading a reconciliation manifest rejects an impact cap below one.</summary>
    [Fact]
    public async Task GitHub_roadmap_reconciliation_load_rejects_an_impact_cap_below_one()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
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

    /// <summary>Verifies that import does nothing when the GitHub integration is disabled.</summary>
    [Fact]
    public async Task GitHub_import_respects_disabled_integration()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe("sharedkernel-repo: GitHub import is disabled in roadmap/config.json." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that import fails when the intake configuration is missing.</summary>
    [Fact]
    public async Task GitHub_import_requires_the_intake_configuration()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapSnapshotTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 4,
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
            """,
            []);
        var config = JsonNode.Parse(workspace.ReadFile("roadmap/config.json"))!.AsObject();
        config["integrations"]!["github"]!.AsObject().Remove("intake").ShouldBeTrue();
        workspace.WriteFile("roadmap/config.json", config.ToJsonString());
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe("sharedkernel-repo: roadmap/config.json must define integrations.github.intake before GitHub import." + Environment.NewLine);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that import fails unless exactly one reconciliation manifest exists.</summary>
    [Theory]
    [InlineData(0, "One GitHub reconciliation manifest is required under roadmap/reconciliation.")]
    [InlineData(2, "Exactly one GitHub reconciliation manifest is required under roadmap/reconciliation.")]
    public async Task GitHub_import_requires_exactly_one_reconciliation_manifest(int manifestCount, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        for (var index = 0; index < manifestCount; index++)
        {
            workspace.WriteFile($"roadmap/reconciliation/open-issues-{index.ToString(CultureInfo.InvariantCulture)}.json", "{}");
        }

        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe($"sharedkernel-repo: {expectedError}{Environment.NewLine}");
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies that import applies the configured identity, scoring, theme, and status defaults to new items.</summary>
    [Fact]
    public async Task GitHub_import_uses_configured_identity_scoring_theme_and_status_defaults()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var config = workspace.ReadFile("roadmap/config.json")
            .Replace("\"itemIdPrefix\": \"RM\"", "\"itemIdPrefix\": \"RD\"", StringComparison.Ordinal)
            .Replace("\"theme\": \"repo-operations\"", "\"theme\": \"custom-theme\"", StringComparison.Ordinal)
            .Replace("\"openStatus\": \"proposed\"", "\"openStatus\": \"ready\"", StringComparison.Ordinal)
            .Replace("\"closedStatus\": \"done\"", "\"closedStatus\": \"dropped\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/config.json", config);
        var theme = workspace.ReadFile("roadmap/themes/repo-operations.json")
            .Replace("\"id\": \"repo-operations\"", "\"id\": \"custom-theme\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/themes/repo-operations.json", theme);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json")
            .Replace("\"id\": \"RM-001\"", "\"id\": \"RD-001\"", StringComparison.Ordinal)
            .Replace("\"theme\": \"repo-operations\"", "\"theme\": \"custom-theme\"", StringComparison.Ordinal);
        workspace.DeleteFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RD-001-roadmap-gitops.json", defaultItem);
        workspace.WriteFile(
            "roadmap/order.json",
            """
            {
              "ordering": "lower order values are higher priority",
              "items": ["RD-001"]
            }
            """);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Configured open item",
                "OPEN",
                labels: ["type: feature"],
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(99, "CLOSED")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                99,
                "Configured closed support",
                "CLOSED",
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
                "impactCap": 4,
                "confidence": 0.5,
                "effort": { "type: epic": 9, "type: feature": 6, "type: enabler": 4, "type: docs": 2, "type: chore": 1, "default": 4 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "blockerEdges": [
                { "blocker": 99, "blockerState": "CLOSED", "blocked": 100, "blockedState": "OPEN" }
              ],
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
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        using var openItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RD-040-github-100.json"));
        using var closedItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RD-041-github-99.json"));
        var openItemRoot = openItem.RootElement;
        var closedItemRoot = closedItem.RootElement;

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        openItemRoot.GetProperty("status").GetString().ShouldBe("ready");
        openItemRoot.GetProperty("theme").GetString().ShouldBe("custom-theme");
        openItemRoot.GetProperty("scoring").GetProperty("reach").GetDecimal().ShouldBe(2m);
        openItemRoot.GetProperty("scoring").GetProperty("impact").GetDecimal().ShouldBe(1m);
        openItemRoot.GetProperty("scoring").GetProperty("confidence").GetDecimal().ShouldBe(0.5m);
        openItemRoot.GetProperty("scoring").GetProperty("effort").GetDecimal().ShouldBe(6m);
        closedItemRoot.GetProperty("status").GetString().ShouldBe("dropped");
        closedItemRoot.GetProperty("theme").GetString().ShouldBe("custom-theme");
        closedItemRoot.GetProperty("triage").GetString().ShouldBe("untriaged");
    }
}
