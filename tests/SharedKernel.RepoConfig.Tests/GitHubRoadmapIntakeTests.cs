using System.Globalization;
using System.Net;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapIntakeTests
{
    [Fact]
    public async Task Intake_github_does_not_link_an_external_parent_with_a_matching_issue_number()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "External child",
                "OPEN",
                parent: GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN", "other/repository")),
            GitHubRoadmapSnapshotTestOperations.Issue(200, "Local issue", "OPEN")
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
              "needsHuman": [100, 200],
              "integrity": {
                "expectedIssueCount": 2,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 2,
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
        using var childItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        childItem.RootElement.TryGetProperty("parent", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Intake_github_rejects_a_manifest_without_snapshot_digest_before_network()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
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
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("GitHub intake requires snapshotDigest; run reconcile github --apply first.", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Intake_github_defaults_to_dry_run_and_fetches_only_safe_metadata()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(300, "Three hundred", "OPEN", labels: ["type: docs"]),
            GitHubRoadmapSnapshotTestOperations.Issue(100, "One hundred", "OPEN", labels: ["type: enabler"])
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
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [300, 100],
              "integrity": {
                "expectedIssueCount": 2,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 2,
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
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = output.ToString();
        var errorText = error.ToString();
        var stateAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        var request = handler.LastRequest.ShouldNotBeNull();
        var requestBody = request.Body.ShouldNotBeNull();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("dry-run: GitHub intake would add 2 open roadmap items.", StringComparison.Ordinal);
        errorText.ShouldBe(string.Empty);
        stateAfter.ShouldBe(stateBefore);
        request.Message.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe("/graphql");
        requestBody.ShouldContain("number", StringComparison.Ordinal);
        requestBody.ShouldContain("title", StringComparison.Ordinal);
        requestBody.ShouldContain("state", StringComparison.Ordinal);
        requestBody.ShouldContain("labels", StringComparison.Ordinal);
        requestBody.ShouldContain("parent", StringComparison.Ordinal);
        requestBody.ShouldNotContain("body", StringComparison.Ordinal);
        requestBody.ShouldNotContain("comments", StringComparison.Ordinal);
        requestBody.ShouldNotContain("token", StringComparison.Ordinal);
        requestBody.ShouldNotContain("mutation", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Intake_github_apply_allocates_links_prioritizes_and_is_idempotent()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var firstOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var firstError = new StringWriter(CultureInfo.InvariantCulture);
        using var secondOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var secondError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                300,
                "Documentation work",
                "OPEN",
                labels: ["type: docs"],
                parent: GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN"),
                blockedBy:
                [
                    GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN"),
                    GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN")
                ]),
            GitHubRoadmapSnapshotTestOperations.Issue(997, "Reviewed title must remain untouched", "OPEN", labels: ["type: epic"]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                200,
                "Enabler work",
                "OPEN",
                labels: ["type: enabler"],
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(99, "CLOSED")],
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(300, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Epic work",
                "OPEN",
                labels: ["type: epic"],
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(98, "CLOSED")],
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(300, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                98,
                "Closed support one",
                "CLOSED",
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                99,
                "Closed support two",
                "CLOSED",
                labels: ["type: chore"],
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN")])
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
              "blockerEdges": [
                { "blocker": 98, "blockerState": "CLOSED", "blocked": 100, "blockedState": "OPEN" },
                { "blocker": 99, "blockerState": "CLOSED", "blocked": 200, "blockedState": "OPEN" },
                { "blocker": 100, "blockerState": "OPEN", "blocked": 300, "blockedState": "OPEN" },
                { "blocker": 200, "blockerState": "OPEN", "blocked": 300, "blockedState": "OPEN" }
              ],
              "directCanonicalPrimaries": [{ "issue": 997, "roadmapItem": "RM-001" }],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [300, 100, 200],
              "integrity": {
                "expectedIssueCount": 4,
                "expectedDirectCanonicalPrimaryCount": 1,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 3,
                "expectedBlockerEdgeCount": 4,
                "dispositionsAreDisjoint": true,
                "dispositionsCoverSnapshot": true
              }
            }
            """,
            snapshot);
        var reviewedItemBefore = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        using var firstHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(firstHandler, snapshot);
        using var firstHttpClient = new HttpClient(firstHandler);

        // Act
        var firstExitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], firstOutput, firstError, workspace.RootPath, firstHttpClient, TestContext.Current.CancellationToken);
        var stateAfterFirstApply = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var firstImportedItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        using var dependentItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-020-github-300.json"));
        using var closedSupportItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-021-github-98.json"));
        using var orderDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/order.json"));
        using var secondHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(secondHandler, snapshot);
        using var secondHttpClient = new HttpClient(secondHandler);
        var secondExitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], secondOutput, secondError, workspace.RootPath, secondHttpClient, TestContext.Current.CancellationToken);
        var stateAfterSecondApply = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        var firstItemRoot = firstImportedItem.RootElement;
        var dependentRoot = dependentItem.RootElement;
        var closedSupportRoot = closedSupportItem.RootElement;
        var orderedItems = orderDocument.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var dependentBlockers = dependentRoot.GetProperty("blockedBy").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var closedSupportBlocks = closedSupportRoot.GetProperty("blocks").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

        // Assert
        firstExitCode.ShouldBe(0);
        firstOutput.ToString().ShouldContain("intake: added 3 open roadmap items.", StringComparison.Ordinal);
        firstError.ToString().ShouldBe(string.Empty);
        workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").ShouldBe(reviewedItemBefore);
        firstItemRoot.GetProperty("id").GetString().ShouldBe("RM-018");
        firstItemRoot.GetProperty("integrations").GetProperty("github").GetProperty("issue").GetInt32().ShouldBe(100);
        firstItemRoot.GetProperty("scoring").GetProperty("effort").GetDecimal().ShouldBe(8m);
        dependentRoot.GetProperty("parent").GetString().ShouldBe("RM-018");
        dependentRoot.GetProperty("scoring").GetProperty("impact").GetDecimal().ShouldBe(3m);
        dependentRoot.GetProperty("scoring").GetProperty("effort").GetDecimal().ShouldBe(2m);
        dependentBlockers.ShouldBe(["RM-018", "RM-019"]);
        closedSupportRoot.GetProperty("status").GetString().ShouldBe("done");
        closedSupportRoot.GetProperty("triage").GetString().ShouldBe("untriaged");
        closedSupportBlocks.ShouldBe(["RM-018"]);
        orderedItems.ShouldBe(["RM-001", "RM-019", "RM-018", "RM-020"]);
        secondExitCode.ShouldBe(0);
        secondOutput.ToString().ShouldContain("intake: roadmap already matches the reconciliation manifest.", StringComparison.Ordinal);
        secondError.ToString().ShouldBe(string.Empty);
        stateAfterSecondApply.ShouldBe(stateAfterFirstApply);
    }

    [Fact]
    public async Task Intake_github_rejects_issue_set_digest_drift_without_writing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        IReadOnlyList<GitHubRoadmapReconcileIssue> expectedSnapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Expected issue", "OPEN")
        ];
        IReadOnlyList<GitHubRoadmapReconcileIssue> changedSnapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(101, "Unexpected issue", "OPEN")
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
            expectedSnapshot);
        var stateBefore = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, changedSnapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var errorText = error.ToString();
        var stateAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        errorText.ShouldContain("GitHub issue snapshot metadata does not match the reconciliation manifest.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }

    [Fact]
    public async Task Intake_github_rejects_pull_requests_without_writing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        IReadOnlyList<GitHubRoadmapReconcileIssue> expectedSnapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Open issue", "OPEN"),
            GitHubRoadmapSnapshotTestOperations.Issue(99, "Closed support", "CLOSED")
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
              "blockerEdges": [{ "blocker": 99, "blockerState": "CLOSED", "blocked": 100, "blockedState": "OPEN" }],
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
            expectedSnapshot);
        var stateBefore = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {
              "data": {
                "repository": {
                  "defaultBranchRef": { "target": { "oid": "1111111111111111111111111111111111111111" } },
                  "issues": {
                    "nodes": [
                      {
                        "__typename": "Issue",
                        "number": 100,
                        "title": "Open issue",
                        "state": "OPEN",
                        "labels": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "parent": null,
                        "subIssues": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blockedBy": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blocking": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } }
                      },
                      { "__typename": "PullRequest" }
                    ],
                    "pageInfo": { "hasNextPage": false, "endCursor": null }
                  }
                }
              }
            }
            """);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var errorText = error.ToString();
        var stateAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        errorText.ShouldContain("GitHub reconciliation rejected a pull request or unsupported node.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }

    [Fact]
    public async Task Intake_github_transitions_a_declared_imported_item_to_closed_support()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var firstOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var firstError = new StringWriter(CultureInfo.InvariantCulture);
        using var secondOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var secondError = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(101, "New open issue", "OPEN"),
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed imported issue", "CLOSED")
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
              "closedItemTransitions": [{ "issue": 100, "roadmapItem": "RM-018" }],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [101],
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
        using var firstHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(firstHandler, snapshot);
        using var firstHttpClient = new HttpClient(firstHandler);

        // Act
        var firstExitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], firstOutput, firstError, workspace.RootPath, firstHttpClient, TestContext.Current.CancellationToken);
        var stateAfterFirstApply = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var transitionedItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        using var newOpenItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-019-github-101.json"));
        using var orderDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/order.json"));
        using var secondHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(secondHandler, snapshot);
        using var secondHttpClient = new HttpClient(secondHandler);
        var secondExitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], secondOutput, secondError, workspace.RootPath, secondHttpClient, TestContext.Current.CancellationToken);
        var stateAfterSecondApply = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        var transitionedRoot = transitionedItem.RootElement;
        var newOpenRoot = newOpenItem.RootElement;
        var transitionedBlocks = transitionedRoot.GetProperty("blocks").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var newOpenBlockers = newOpenRoot.GetProperty("blockedBy").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var orderedItems = orderDocument.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

        // Assert
        firstExitCode.ShouldBe(0);
        firstOutput.ToString().ShouldContain("intake: transitioned 1 imported roadmap item to closed support.", StringComparison.Ordinal);
        firstError.ToString().ShouldBe(string.Empty);
        transitionedRoot.GetProperty("status").GetString().ShouldBe("done");
        transitionedRoot.GetProperty("triage").GetString().ShouldBe("untriaged");
        transitionedRoot.TryGetProperty("order", out _).ShouldBeFalse();
        transitionedRoot.TryGetProperty("scoring", out _).ShouldBeFalse();
        transitionedBlocks.ShouldBe([]);
        newOpenBlockers.ShouldBe([]);
        orderedItems.ShouldBe(["RM-001", "RM-019"]);
        secondExitCode.ShouldBe(0);
        secondOutput.ToString().ShouldContain("intake: roadmap already matches the reconciliation manifest.", StringComparison.Ordinal);
        secondError.ToString().ShouldBe(string.Empty);
        stateAfterSecondApply.ShouldBe(stateAfterFirstApply);
    }

    [Fact]
    public async Task Intake_github_preserves_exact_blocker_links_during_a_closed_transition()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                101,
                "New open issue",
                "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(100, "CLOSED")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Closed imported issue",
                "CLOSED",
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN")])
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
              "blockerEdges": [{ "blocker": 100, "blockerState": "CLOSED", "blocked": 101, "blockedState": "OPEN" }],
              "closedItemTransitions": [{ "issue": 100, "roadmapItem": "RM-018" }],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [101],
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
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        using var transitionedItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        using var newOpenItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-019-github-101.json"));
        using var orderDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/order.json"));
        var transitionedBlocks = transitionedItem.RootElement.GetProperty("blocks").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var newOpenBlockers = newOpenItem.RootElement.GetProperty("blockedBy").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var orderedItems = orderDocument.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        transitionedBlocks.ShouldBe(["RM-019"]);
        newOpenBlockers.ShouldBe(["RM-018"]);
        orderedItems.ShouldBe(["RM-001", "RM-019"]);
    }

    [Fact]
    public async Task Intake_github_rejects_an_undeclared_imported_closed_transition_without_writing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                101,
                "New open issue",
                "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(100, "CLOSED")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Closed imported issue",
                "CLOSED",
                blocking: [GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN")])
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
              "blockerEdges": [{ "blocker": 100, "blockerState": "CLOSED", "blocked": 101, "blockedState": "OPEN" }],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [101],
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
        error.ToString().ShouldContain("GitHub closed support issue #100 must be declared as a closed item transition.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }

    [Fact]
    public async Task Intake_github_rejects_a_declared_transition_without_an_existing_mapping()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(101, "New open issue", "OPEN"),
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed issue", "CLOSED")
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
              "closedItemTransitions": [{ "issue": 100, "roadmapItem": "RM-018" }],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [101],
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
        error.ToString().ShouldContain("GitHub closed item transition requires an existing exact mapping: #100 -> RM-018.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }

    [Fact]
    public async Task Intake_github_rejects_a_closed_transition_for_a_reviewed_item()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        var reviewedItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json")
            .Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 100 } },\n  \"labels\": [", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", reviewedItem);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed reviewed issue", "CLOSED")
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
              "closedItemTransitions": [{ "issue": 100, "roadmapItem": "RM-001" }],
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
            snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("GitHub closed item transition must reference an intake-generated item: #100 -> RM-001.", StringComparison.Ordinal);
        workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").ShouldBe(reviewedItem);
    }

    [Fact]
    public async Task Intake_github_rejects_a_declared_transition_with_a_different_roadmap_item()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed issue", "CLOSED")
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
              "closedItemTransitions": [{ "issue": 100, "roadmapItem": "RM-019" }],
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
        error.ToString().ShouldContain("GitHub closed item transition requires an existing exact mapping: #100 -> RM-019.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }

    [Fact]
    public async Task Intake_github_replaces_imported_relationships_with_exact_github_metadata()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.AddImportedOpenPairWithStaleRelationship(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Imported parent",
                "OPEN",
                subIssues: [GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                101,
                "Imported child",
                "OPEN",
                parent: GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN"))
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
              "needsHuman": [100, 101],
              "integrity": {
                "expectedIssueCount": 2,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 2,
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
        using var parentItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        using var childItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-019-github-101.json"));
        var parentBlocks = parentItem.RootElement.GetProperty("blocks").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        var childBlockers = childItem.RootElement.GetProperty("blockedBy").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("intake: updated 2 existing roadmap items with exact GitHub metadata.", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
        parentBlocks.ShouldBeEmpty();
        childBlockers.ShouldBeEmpty();
        childItem.RootElement.GetProperty("parent").GetString().ShouldBe("RM-018");
    }

    [Fact]
    public async Task Intake_github_links_only_mapped_open_parents_from_a_digest_snapshot()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        var repository = "owner/repository";
        var issue100 = new GitHubRoadmapReconcileIssue(
            100,
            "Child issue",
            "OPEN",
            [],
            new GitHubRoadmapReconcileRelation(200, repository, "OPEN"),
            [],
            [],
            []);
        var issue200 = new GitHubRoadmapReconcileIssue(
            200,
            "Open parent",
            "OPEN",
            [],
            new GitHubRoadmapReconcileRelation(300, repository, "CLOSED"),
            [new GitHubRoadmapReconcileRelation(100, repository, "OPEN")],
            [],
            []);
        var issue300 = new GitHubRoadmapReconcileIssue(
            300,
            "Closed terminal parent",
            "CLOSED",
            [],
            null,
            [new GitHubRoadmapReconcileRelation(200, repository, "OPEN")],
            [],
            []);
        var snapshotDigest = GitHubIssueSnapshotDigest.Compute([issue100, issue200, issue300]);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "snapshotDigest": "__DIGEST__",
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
              "needsHuman": [100, 200],
              "integrity": {
                "expectedIssueCount": 2,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 2,
                "expectedBlockerEdgeCount": 0,
                "dispositionsAreDisjoint": true,
                "dispositionsCoverSnapshot": true
              }
            }
            """.Replace("__DIGEST__", snapshotDigest, StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {
              "data": {
                "repository": {
                  "defaultBranchRef": { "target": { "oid": "1111111111111111111111111111111111111111" } },
                  "issues": {
                    "nodes": [
                      {
                        "__typename": "Issue",
                        "number": 100,
                        "title": "Child issue",
                        "state": "OPEN",
                        "labels": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "parent": { "number": 200, "state": "OPEN", "repository": { "nameWithOwner": "owner/repository" } },
                        "subIssues": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blockedBy": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blocking": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } }
                      },
                      {
                        "__typename": "Issue",
                        "number": 200,
                        "title": "Open parent",
                        "state": "OPEN",
                        "labels": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "parent": { "number": 300, "state": "CLOSED", "repository": { "nameWithOwner": "owner/repository" } },
                        "subIssues": { "nodes": [{ "number": 100, "state": "OPEN", "repository": { "nameWithOwner": "owner/repository" } }], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blockedBy": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blocking": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } }
                      },
                      {
                        "__typename": "Issue",
                        "number": 300,
                        "title": "Closed terminal parent",
                        "state": "CLOSED",
                        "labels": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "parent": null,
                        "subIssues": { "nodes": [{ "number": 200, "state": "OPEN", "repository": { "nameWithOwner": "owner/repository" } }], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blockedBy": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blocking": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } }
                      }
                    ],
                    "pageInfo": { "hasNextPage": false, "endCursor": null }
                  }
                }
              }
            }
            """);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("intake: added 2 open roadmap items.", StringComparison.Ordinal);
        error.ToString().ShouldBe(string.Empty);
        using var childItem = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-018-github-100.json"));
        childItem.RootElement.GetProperty("parent").GetString().ShouldBe("RM-019");
    }

    [Fact]
    public async Task Intake_github_rejects_snapshot_metadata_drift_without_writing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        (await RepoConfigToolApplication.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken)).ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
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
              "snapshotDigest": "0000000000000000000000000000000000000000000000000000000000000000",
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
            """);
        var stateBefore = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {
              "data": {
                "repository": {
                  "defaultBranchRef": { "target": { "oid": "1111111111111111111111111111111111111111" } },
                  "issues": {
                    "nodes": [
                      {
                        "__typename": "Issue",
                        "number": 100,
                        "title": "Changed after reconciliation",
                        "state": "OPEN",
                        "labels": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "parent": null,
                        "subIssues": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blockedBy": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } },
                        "blocking": { "nodes": [], "pageInfo": { "hasNextPage": false, "endCursor": null } }
                      }
                    ],
                    "pageInfo": { "hasNextPage": false, "endCursor": null }
                  }
                }
              }
            }
            """);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolApplication.Run(["intake", "github", "--apply", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var stateAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("GitHub issue snapshot metadata does not match the reconciliation manifest.", StringComparison.Ordinal);
        stateAfter.ShouldBe(stateBefore);
    }
}
