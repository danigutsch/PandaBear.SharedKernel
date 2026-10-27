namespace PandaBear.RepoConfig.Tests;

/// <summary>Drives the roadmap automation service against a temporary workspace with scripted GitHub snapshots and a fake Git repository.</summary>
internal static class RoadmapAutomationTestSetup
{
    /// <summary>Applies <paramref name="plan"/> to the workspace using a scripted snapshot and a Git repository whose HEAD is <paramref name="head"/>.</summary>
    /// <param name="workspace">The workspace the plan is applied to.</param>
    /// <param name="plan">The plan whose digest is applied for <c>owner/repository</c>.</param>
    /// <param name="snapshot">The GitHub issues returned by the scripted snapshot.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <param name="head">The commit SHA the fake Git repository reports as HEAD.</param>
    public static async Task ApplyPlan(
        TemporaryRepoConfigWorkspace workspace,
        RoadmapAutomationPlan plan,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        CancellationToken cancellationToken,
        string head = RoadmapAutomationTestPlan.BaseSha)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot, head);
        using var httpClient = new HttpClient(handler);
        var service = new RoadmapAutomationService(
            workspace.RootPath,
            httpClient,
            RoadmapAutomationTestGit.Create(head: head));
        await service.ApplyPlan(plan.PlanDigest, "owner/repository", cancellationToken);
    }

    /// <summary>Initializes the workspace's repo config and writes a reconciliation file for <c>owner/repository</c> that marks issue 100 as needing a human, then adds an imported open item.</summary>
    /// <param name="workspace">The workspace to initialize.</param>
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

    /// <summary>Creates the automation plan for run 42 of <c>owner/repository</c> from a scripted snapshot.</summary>
    /// <param name="workspace">The workspace the plan is created for.</param>
    /// <param name="snapshot">The GitHub issues returned by the scripted snapshot.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
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

    /// <summary>Configures the workspace, creates and applies a plan, refreshes the reconciliation, and seals the result against a Git status.</summary>
    /// <param name="workspace">The workspace to configure and seal.</param>
    /// <param name="snapshot">The GitHub issues returned by every scripted snapshot.</param>
    /// <param name="status">The Git status bytes the sealing step reads.</param>
    /// <param name="artifactDigest">The digest of the artifact bound into the seal.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <param name="configure">An optional action that changes the workspace after <see cref="Configure"/> and before the plan is created.</param>
    public static async Task<(RoadmapAutomationPlan Plan, RoadmapAutomationResultSeal Seal)> CreateSealedResult(
        TemporaryRepoConfigWorkspace workspace,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        byte[] status,
        string artifactDigest,
        CancellationToken cancellationToken,
        Action<TemporaryRepoConfigWorkspace>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(status);
        Configure(workspace);
        configure?.Invoke(workspace);
        var plan = await CreatePlan(workspace, snapshot, cancellationToken);
        await ApplyPlan(workspace, plan, snapshot, cancellationToken);
        await RefreshReconciliation(workspace, snapshot, cancellationToken);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot);
        using var httpClient = new HttpClient(handler);
        var service = new RoadmapAutomationService(
            workspace.RootPath,
            httpClient,
            RoadmapAutomationTestGit.Create(status));
        var seal = await service.SealResult(plan.PlanDigest, artifactDigest, "owner/repository", cancellationToken);
        return (plan, seal);
    }

    /// <summary>Re-applies the GitHub roadmap reconciler to the workspace using a scripted snapshot at <paramref name="head"/>.</summary>
    /// <param name="workspace">The workspace whose reconciliation is rewritten.</param>
    /// <param name="snapshot">The GitHub issues returned by the scripted snapshot.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <param name="head">The commit SHA the scripted snapshot reports.</param>
    public static async Task RefreshReconciliation(
        TemporaryRepoConfigWorkspace workspace,
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot,
        CancellationToken cancellationToken,
        string head = RoadmapAutomationTestPlan.BaseSha)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(snapshot);
        using var handler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(handler, snapshot, head);
        using var httpClient = new HttpClient(handler);
        var reconciler = new GitHubRoadmapReconciler(RoadmapProject.Load(workspace.RootPath), httpClient);
        _ = await reconciler.Apply(cancellationToken);
    }
}
