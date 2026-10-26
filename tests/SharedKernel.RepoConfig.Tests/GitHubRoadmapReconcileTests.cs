using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubRoadmapReconcileTests
{
    [Fact]
    public async Task GitHub_reconcile_rejects_a_missing_local_open_relation_target()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Missing open child",
                "OPEN",
                subIssues: [GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN")])
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("GitHub reconciliation local open relationship target is missing", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_reconcile_distinguishes_same_number_relations_from_different_repositories()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapReconcileTestOperations.ConfigureEmptySeed(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Parent",
                "OPEN",
                subIssues:
                [
                    GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN"),
                    GitHubRoadmapSnapshotTestOperations.Relation(101, "OPEN", "other/repository")
                ]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                101,
                "Local child",
                "OPEN",
                parent: GitHubRoadmapSnapshotTestOperations.Relation(100, "OPEN"))
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        error.ToString().ShouldBe(string.Empty);
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task GitHub_reconcile_digest_excludes_seed_only_closed_issues()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var reconcileOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var reconcileError = new StringWriter(CultureInfo.InvariantCulture);
        using var importOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var importError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "rules": ["Only exact GitHub blocker relationships may become canonical blockedBy and blocks links."],
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
            """);
        IReadOnlyList<GitHubRoadmapReconcileIssue> reconcileSnapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Open issue", "OPEN"),
            GitHubRoadmapSnapshotTestOperations.Issue(99, "Seed-only closed issue", "CLOSED")
        ];
        using var reconcileHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(reconcileHandler, reconcileSnapshot);
        using var reconcileClient = new HttpClient(reconcileHandler);
        IReadOnlyList<GitHubRoadmapReconcileIssue> importSnapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Open issue", "OPEN")
        ];
        using var importHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(importHandler, importSnapshot);
        using var importClient = new HttpClient(importHandler);

        // Act
        var reconcileExitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], reconcileOutput, reconcileError, workspace.RootPath, reconcileClient, TestContext.Current.CancellationToken);
        var importExitCode = await RepoConfigToolRunner.Run(["github", "import", "--root", workspace.RootPath], importOutput, importError, workspace.RootPath, importClient, TestContext.Current.CancellationToken);

        // Assert
        reconcileExitCode.ShouldBe(0);
        reconcileError.ToString().ShouldBe(string.Empty);
        importExitCode.ShouldBe(0);
        importError.ToString().ShouldBe(string.Empty);
        importOutput.ToString().ShouldContain("preview: GitHub import would add 1 open roadmap items.", StringComparison.Ordinal);
    }



    [Fact]
    public async Task GitHub_reconcile_preserves_qualified_cross_repository_blocker_metadata()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "rules": ["Only exact GitHub blocker relationships may become canonical blockedBy and blocks links."],
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
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "Cross-repository blocker",
                "OPEN",
                blockedBy: [GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN", "other/repository")])
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
        error.ToString().ShouldBe(string.Empty);
        using var manifest = JsonDocument.Parse(workspace.ReadFile("roadmap/reconciliation/open-issues-test.json"));
        var externalEdges = manifest.RootElement.GetProperty("externalBlockerEdges").EnumerateArray().ToArray();
        externalEdges.Length.ShouldBe(1);
        var blockerRepository = externalEdges[0].GetProperty("blocker").GetProperty("repository").GetString();
        blockerRepository.ShouldBe("other/repository");
        var blockerIssue = externalEdges[0].GetProperty("blocker").GetProperty("issue").GetInt32();
        blockerIssue.ShouldBe(200);
        var blockedRepository = externalEdges[0].GetProperty("blocked").GetProperty("repository").GetString();
        blockedRepository.ShouldBe("owner/repository");
        var blockedIssue = externalEdges[0].GetProperty("blocked").GetProperty("issue").GetInt32();
        blockedIssue.ShouldBe(100);
        var externalCount = manifest.RootElement.GetProperty("integrity").GetProperty("expectedExternalBlockerEdgeCount").GetInt32();
        externalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GitHub_reconcile_force_rejects_a_manifest_changed_during_the_request()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot = [];
        GitHubRoadmapSnapshotTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "rules": ["Only exact GitHub blocker relationships may become canonical blockedBy and blocks links."],
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
            snapshot);
        const string manifestPath = "roadmap/reconciliation/open-issues-test.json";
        var concurrentContent = workspace.ReadFile(manifestPath) + Environment.NewLine;
        using var handler = new BeforeResponseHttpMessageHandler(
            () => GitHubRoadmapSnapshotTestOperations.CreateOpenIssuesResponse(snapshot),
            () => workspace.WriteFile(manifestPath, concurrentContent));
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("changed after the write plan was created", StringComparison.Ordinal);
        workspace.ReadFile(manifestPath).ShouldBe(concurrentContent);
    }

    [Fact]
    public async Task GitHub_reconcile_reports_the_seed_manifest_inputs_when_missing()
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

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldContain("GitHub reconciliation requires one seed manifest under roadmap/reconciliation/open-issues-*.json.", StringComparison.Ordinal);
        error.ToString().ShouldContain("Required reviewed inputs: mechanicalPriorityOverride, directCanonicalPrimaries, and closedItemTransitions.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_reconcile_treats_an_external_parent_as_needs_human_without_parent_chain_exit()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "repositoryCommit": "0000000000000000000000000000000000000000",
              "source": "GitHub GraphQL repository.issues OPEN snapshot with parent, subissue, label, and blocker metadata.",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "rules": ["Only exact GitHub blocker relationships may become canonical blockedBy and blocks links."],
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
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "External child",
                "OPEN",
                parent: GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN", "other/repository")),
            GitHubRoadmapSnapshotTestOperations.Issue(
                200,
                "Local issue",
                "OPEN",
                subIssues:
                [
                    GitHubRoadmapSnapshotTestOperations.Relation(301, "CLOSED"),
                    GitHubRoadmapSnapshotTestOperations.Relation(302, "CLOSED")
                ]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                301,
                "Closed child",
                "CLOSED",
                parent: GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN"),
                subIssues: [GitHubRoadmapSnapshotTestOperations.Relation(303, "CLOSED")]),
            GitHubRoadmapSnapshotTestOperations.Issue(
                302,
                "Closed child",
                "CLOSED",
                parent: GitHubRoadmapSnapshotTestOperations.Relation(200, "OPEN"))
        ];
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        using var manifest = JsonDocument.Parse(workspace.ReadFile("roadmap/reconciliation/open-issues-test.json"));
        var root = manifest.RootElement;
        var needsHuman = root.GetProperty("needsHuman").EnumerateArray().Select(issue => issue.GetInt32()).ToArray();

        // Assert
        error.ToString().ShouldBe(string.Empty);
        exitCode.ShouldBe(0);
        needsHuman.ShouldBe([100, 200]);
        root.GetProperty("childrenOfCanonicalPrimaries").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GitHub_reconcile_defaults_to_preview_and_requests_only_structural_metadata()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "repositoryCommit": "0000000000000000000000000000000000000000",
              "source": "GitHub GraphQL repository.issues OPEN snapshot with parent, subissue, label, and blocker metadata.",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "rules": ["Only exact GitHub blocker relationships may become canonical blockedBy and blocks links."],
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
                        "title": "Epic one hundred",
                        "state": "OPEN",
                        "labels": { "nodes": [{ "name": "type: epic" }], "pageInfo": { "hasNextPage": false, "endCursor": null } },
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
        handler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateRepositoryCommitResponse());
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--root", workspace.RootPath], output, error, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var stateAfter = GitHubRoadmapIntakeTestOperations.ReadRoadmapState(workspace);
        var request = handler.Requests[0];
        var requestBody = request.Body.ShouldNotBeNull();

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldBe(
            $"preview: GitHub reconciliation would update {Path.Combine("roadmap", "reconciliation", "open-issues-test.json")}.{Environment.NewLine}");
        error.ToString().ShouldBe(string.Empty);
        stateAfter.ShouldBe(stateBefore);
        request.Message.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe("/graphql");
        requestBody.ShouldContain("number", StringComparison.Ordinal);
        requestBody.ShouldContain("title", StringComparison.Ordinal);
        requestBody.ShouldContain("state", StringComparison.Ordinal);
        requestBody.ShouldContain("labels", StringComparison.Ordinal);
        requestBody.ShouldContain("parent", StringComparison.Ordinal);
        requestBody.ShouldContain("subIssues", StringComparison.Ordinal);
        requestBody.ShouldContain("blockedBy", StringComparison.Ordinal);
        requestBody.ShouldContain("blocking", StringComparison.Ordinal);
        requestBody.ShouldContain("states:[OPEN]", StringComparison.Ordinal);
        requestBody.ShouldNotContain("states:[OPEN,CLOSED]", StringComparison.Ordinal);
        requestBody.ShouldNotContain("body", StringComparison.Ordinal);
        requestBody.ShouldNotContain("comments", StringComparison.Ordinal);
        requestBody.ShouldNotContain("token", StringComparison.Ordinal);
        requestBody.ShouldNotContain("mutation", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_reconcile_force_derives_a_stable_manifest()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var firstOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var firstError = new StringWriter(CultureInfo.InvariantCulture);
        using var secondOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var secondError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "repositoryCommit": "0000000000000000000000000000000000000000",
              "source": "GitHub GraphQL repository.issues OPEN snapshot with parent, subissue, label, and blocker metadata.",
              "ruleVersion": "structural-parent-subissue-blocker-v1",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "rules": ["Only exact GitHub blocker relationships may become canonical blockedBy and blocks links."],
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
        using var firstHandler = new TestHttpMessageHandler();
        firstHandler.EnqueueJson(
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
                        "title": "Epic one hundred",
                        "state": "OPEN",
                        "labels": { "nodes": [{ "name": "type: epic" }], "pageInfo": { "hasNextPage": false, "endCursor": null } },
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
        firstHandler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateRepositoryCommitResponse());
        using var firstHttpClient = new HttpClient(firstHandler);

        // Act
        var firstExitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], firstOutput, firstError, workspace.RootPath, firstHttpClient, TestContext.Current.CancellationToken);
        var manifestAfterFirstApply = workspace.ReadFile("roadmap/reconciliation/open-issues-test.json");
        using var manifestDocument = JsonDocument.Parse(manifestAfterFirstApply);
        using var secondHandler = new TestHttpMessageHandler();
        secondHandler.EnqueueJson(
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
                        "title": "Epic one hundred",
                        "state": "OPEN",
                        "labels": { "nodes": [{ "name": "type: epic" }], "pageInfo": { "hasNextPage": false, "endCursor": null } },
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
        secondHandler.Enqueue(GitHubRoadmapSnapshotTestOperations.CreateRepositoryCommitResponse());
        using var secondHttpClient = new HttpClient(secondHandler);
        var secondExitCode = await RepoConfigToolRunner.Run(["github", "reconcile", "--force", "--root", workspace.RootPath], secondOutput, secondError, workspace.RootPath, secondHttpClient, TestContext.Current.CancellationToken);
        var manifestAfterSecondApply = workspace.ReadFile("roadmap/reconciliation/open-issues-test.json");
        var root = manifestDocument.RootElement;
        var structuralRoots = root.GetProperty("unmappedStructuralRoots").EnumerateArray().Select(issue => issue.GetInt32()).ToArray();

        // Assert
        firstExitCode.ShouldBe(0);
        firstOutput.ToString().ShouldBe(
            $"reconcile: updated {Path.Combine("roadmap", "reconciliation", "open-issues-test.json")}.{Environment.NewLine}");
        firstError.ToString().ShouldBe(string.Empty);
        root.GetProperty("repositoryCommit").GetString().ShouldBe("1111111111111111111111111111111111111111");
        structuralRoots.ShouldHaveSingleItem().ShouldBe(100);
        root.GetProperty("needsHuman").GetArrayLength().ShouldBe(0);
        root.GetProperty("integrity").GetProperty("expectedIssueCount").GetInt32().ShouldBe(1);
        root.GetProperty("integrity").GetProperty("expectedUnmappedStructuralRootCount").GetInt32().ShouldBe(1);
        secondExitCode.ShouldBe(0);
        secondOutput.ToString().ShouldContain("reconcile: reconciliation manifest already matches the GitHub snapshot.", StringComparison.Ordinal);
        secondError.ToString().ShouldBe(string.Empty);
        manifestAfterSecondApply.ShouldBe(manifestAfterFirstApply);
    }

    [Fact]
    public async Task Snapshot_rejects_default_branch_movement_between_issue_pages()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"page-2"}}}}}
                """);
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                """);
        }

        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation default branch changed while reading the snapshot.");
    }

    [Fact]
    public async Task Snapshot_rechecks_default_branch_after_all_issue_metadata()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[{"__typename":"Issue","number":100,"title":"Paged relations","state":"OPEN","labels":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}},"parent":null,"subIssues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"relations-2"}},"blockedBy":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}},"blocking":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                """);
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"issue":{"subIssues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}}
                """);
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}}}}}
                """);
        }

        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation default branch changed while reading the snapshot.");
    }

    [Fact]
    public async Task Snapshot_rereads_when_only_the_default_branch_commit_changed()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"2222222222222222222222222222222222222222"}}}}}
            """);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        var snapshot = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);

        // Assert
        snapshot.RepositoryCommit.ShouldBe("2222222222222222222222222222222222222222");
    }

    [Fact]
    public async Task Snapshot_stops_rereading_after_three_default_branch_changes()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        foreach (var (first, second) in new[] { ('1', '2'), ('3', '4'), ('5', '6') })
        {
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"OID"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}
                """.Replace("OID", new string(first, 40), StringComparison.Ordinal));
            handler.EnqueueJson(
                HttpStatusCode.OK,
                """
                {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"OID"}}}}}
                """.Replace("OID", new string(second, 40), StringComparison.Ordinal));
        }

        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation default branch changed while reading the snapshot.");
    }

    [Fact]
    public async Task Snapshot_rejects_a_repeated_issue_page_cursor()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"repeated"}}}}}
            """);
        handler.EnqueueJson(
            HttpStatusCode.OK,
            """
            {"data":{"repository":{"defaultBranchRef":{"target":{"oid":"1111111111111111111111111111111111111111"}},"issues":{"nodes":[],"pageInfo":{"hasNextPage":true,"endCursor":"repeated"}}}}}
            """);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation response repeated an issue page cursor.");
    }

    [Fact]
    public async Task Snapshot_rejects_an_oversized_response()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        };
        response.Content.Headers.ContentLength = 9 * 1024 * 1024;
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository",
            [],
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation response exceeded the allowed size.");
    }

    [Fact]
    public async Task Snapshot_deadline_cancels_a_stalled_request_without_waiting_in_real_time()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider, TimeSpan.FromMinutes(2));
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient, timeProvider);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository", [], TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation snapshot timed out after 120 seconds.");
    }

    [Fact]
    public async Task Snapshot_preserves_caller_cancellation_instead_of_reporting_a_timeout()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var handler = TestHttpMessageHandler.FromException(new OperationCanceledException(cancellation.Token));
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository", [], cancellation.Token);
        var exception = await action.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task Snapshot_rejects_an_oversized_body_even_when_content_length_understates_it()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[(8 * 1024 * 1024) + 1])
        };
        response.Content.Headers.ContentLength = 0;
        using var handler = new TestHttpMessageHandler(response);
        using var httpClient = new HttpClient(handler);
        var client = new GitHubRoadmapReconcileClient(httpClient);

        // Act
        Func<Task> action = async () => _ = await client.ReadSnapshot(
            "owner/repository", [], TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub reconciliation response exceeded the allowed size.");
    }

}
