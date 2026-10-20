namespace SharedKernel.RepoConfig.Tests;

internal static class RoadmapAutomationTestSetup
{
    public static async Task ApplyPlan(
        TemporaryRepoConfigWorkspace workspace,
        RoadmapAutomationPlan plan,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);
        var service = new RoadmapAutomationService(
            workspace.RootPath,
            httpClient,
            RoadmapAutomationTestGit.Create());
        await service.ApplyPlan(plan.PlanDigest, "owner/repository", cancellationToken);
    }

    public static void Configure(TemporaryRepoConfigWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        _ = RepoConfigInitializer.Initialize(workspace.RootPath);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "retrievedOn": "2026-07-17",
              "repositoryCommit": "0000000000000000000000000000000000000000",
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
        GitHubRoadmapIntakeTestOperations.AddImportedOpenItem(workspace);
    }

    public static async Task<RoadmapAutomationPlan> CreatePlan(
        TemporaryRepoConfigWorkspace workspace,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);
        var service = new RoadmapAutomationService(
            workspace.RootPath,
            httpClient,
            RoadmapAutomationTestGit.Create());
        return await service.CreatePlan(42, "owner/repository", cancellationToken);
    }

    public static async Task RefreshReconciliation(
        TemporaryRepoConfigWorkspace workspace,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);
        var reconciler = new GitHubRoadmapReconciler(RoadmapProject.Load(workspace.RootPath), httpClient);
        _ = await reconciler.Apply(cancellationToken);
    }
}
