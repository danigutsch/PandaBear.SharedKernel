using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the roadmap automation service's plan, apply, and result-sealing steps against scripted GitHub snapshots.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class RoadmapAutomationServiceTests
{
    /// <summary>For a closed issue 100 with a hostile title and label, the plan requires exactly the transition to RM-018 and its file contains neither the title text nor the label.</summary>
    [Fact]
    public async Task Plan_carries_the_exact_required_transition_without_untrusted_metadata()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(
                100,
                "untrusted\n::warning::title",
                "CLOSED",
                labels: ["untrusted-label"])
        ];
        // Act
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        var planContent = await File.ReadAllTextAsync(
            RoadmapAutomationService.GetPlanPath(workspace.RootPath),
            TestContext.Current.CancellationToken);

        // Assert
        plan.RequiredClosedTransitions.ShouldBe([new RoadmapClosedItemTransition(100, "RM-018")]);
        plan.HasChanges.ShouldBeTrue();
        planContent.ShouldNotContain("untrusted", StringComparison.Ordinal);
        planContent.ShouldNotContain("title", StringComparison.OrdinalIgnoreCase);
        planContent.ShouldNotContain("labels", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Applying a plan for closed issue 100 leaves exactly one closed-item transition in the reconciliation manifest, issue 100 mapped to RM-018.</summary>
    [Fact]
    public async Task Apply_plan_adds_only_the_approved_transition_after_live_revalidation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);

        // Act
        await RoadmapAutomationTestSetup.ApplyPlan(
            workspace,
            plan,
            snapshot,
            TestContext.Current.CancellationToken);
        using var manifest = JsonDocument.Parse(workspace.ReadFile("roadmap/reconciliation/open-issues-test.json"));
        var transitions = manifest.RootElement.GetProperty("closedItemTransitions").EnumerateArray().ToArray();

        // Assert
        transitions.Length.ShouldBe(1);
        transitions[0].GetProperty("issue").GetInt32().ShouldBe(100);
        transitions[0].GetProperty("roadmapItem").GetString().ShouldBe("RM-018");
    }

    /// <summary>Sealing a result after the plan was applied but before the reconciliation manifest was refreshed throws that the manifest does not match the approved snapshot.</summary>
    [Fact]
    public async Task Result_seal_rejects_an_unrefreshed_reconciliation_manifest()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.ApplyPlan(
            workspace,
            plan,
            snapshot,
            TestContext.Current.CancellationToken);
        using var sealHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(sealHandler, snapshot);
        using var sealClient = new HttpClient(sealHandler);
        var seal = new RoadmapAutomationService(
            workspace.RootPath,
            sealClient,
            RoadmapAutomationTestGit.Create(RoadmapGitStatusTestData.Ordinary(
                "roadmap/reconciliation/open-issues-test.json")));

        // Act
        Func<Task> action = async () => _ = await seal.SealResult(
            plan.PlanDigest,
            new string('c', 64),
            "owner/repository",
            TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Roadmap automation reconciliation manifest does not match the approved snapshot.");
    }

    /// <summary>After the manifest is refreshed, sealing a result whose only changed file is the reconciliation file yields a seal with changes, one path, and a 64-character result digest.</summary>
    [Fact]
    public async Task Result_seal_round_trip_revalidates_the_refreshed_manifest_and_confined_diff()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.ApplyPlan(
            workspace,
            plan,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.RefreshReconciliation(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        var status = RoadmapGitStatusTestData.Ordinary("roadmap/reconciliation/open-issues-test.json");
        using var sealHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(sealHandler, snapshot);
        using var sealClient = new HttpClient(sealHandler);
        var sealing = new RoadmapAutomationService(
            workspace.RootPath,
            sealClient,
            RoadmapAutomationTestGit.Create(status));
        var artifactDigest = new string('c', 64);

        // Act
        var seal = await sealing.SealResult(
            plan.PlanDigest,
            artifactDigest,
            "owner/repository",
            TestContext.Current.CancellationToken);
        using var verifyHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(verifyHandler, snapshot);
        using var verifyClient = new HttpClient(verifyHandler);
        var verification = new RoadmapAutomationService(
            workspace.RootPath,
            verifyClient,
            RoadmapAutomationTestGit.Create(status));
        await verification.VerifyResult(
            plan.PlanDigest,
            artifactDigest,
            seal.ResultDigest,
            "owner/repository",
            TestContext.Current.CancellationToken);

        // Assert
        seal.HasChanges.ShouldBeTrue();
        seal.PathCount.ShouldBe(1);
        seal.ResultDigest.Length.ShouldBe(64);
    }

    /// <summary>When HEAD moved to another SHA after planning, the plan keeps the original base SHA while the seal records the new HEAD.</summary>
    [Fact]
    public async Task Result_seal_binds_to_the_current_head_when_main_moved_after_planning()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED")
        ];
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.ApplyPlan(
            workspace,
            plan,
            snapshot,
            TestContext.Current.CancellationToken,
            RoadmapAutomationTestPlan.DifferentSha);
        await RoadmapAutomationTestSetup.RefreshReconciliation(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken,
            RoadmapAutomationTestPlan.DifferentSha);
        using var sealHandler = new TestHttpMessageHandler();
        GitHubRoadmapSnapshotTestOperations.Enqueue(sealHandler, snapshot, RoadmapAutomationTestPlan.DifferentSha);
        using var sealClient = new HttpClient(sealHandler);
        var sealing = new RoadmapAutomationService(
            workspace.RootPath,
            sealClient,
            RoadmapAutomationTestGit.Create(
                RoadmapGitStatusTestData.Ordinary("roadmap/reconciliation/open-issues-test.json"),
                RoadmapAutomationTestPlan.DifferentSha));

        // Act
        var seal = await sealing.SealResult(
            plan.PlanDigest,
            new string('c', 64),
            "owner/repository",
            TestContext.Current.CancellationToken);

        // Assert
        plan.BaseSha.ShouldBe(RoadmapAutomationTestPlan.BaseSha);
        seal.BaseSha.ShouldBe(RoadmapAutomationTestPlan.DifferentSha);
    }

    /// <summary>The single required transition in the plan has the close reason <c>NOT_PLANNED</c> or <c>COMPLETED</c> as given, and <c>COMPLETED</c> when the issue has no state reason.</summary>
    /// <param name="stateReason">The GitHub state reason of the closed issue, or <see langword="null"/>.</param>
    /// <param name="expected">The close reason the plan must carry.</param>
    [Theory]
    [InlineData("NOT_PLANNED", "NOT_PLANNED")]
    [InlineData("COMPLETED", "COMPLETED")]
    [InlineData(null, "COMPLETED")]
    public async Task Plan_carries_the_close_outcome_of_each_required_transition(string? stateReason, string expected)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Closed item", "CLOSED") with { StateReason = stateReason }
        ];

        // Act
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);

        // Assert
        plan.RequiredClosedTransitions.ShouldHaveSingleItem().CloseReason.ShouldBe(expected);
    }

    /// <summary>Applying a plan for an issue closed as <c>NOT_PLANNED</c> and refreshing the manifest records one transition to RM-018 with <c>closeReason</c> set to <c>NOT_PLANNED</c>.</summary>
    [Fact]
    public async Task Apply_plan_records_a_not_planned_close_outcome_in_the_manifest_only_when_it_is_not_completed()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        RoadmapAutomationTestSetup.Configure(workspace);
        IReadOnlyList<GitHubRoadmapReconcileIssue> snapshot =
        [
            GitHubRoadmapSnapshotTestOperations.Issue(100, "Dropped item", "CLOSED") with { StateReason = "NOT_PLANNED" }
        ];
        var plan = await RoadmapAutomationTestSetup.CreatePlan(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);

        // Act
        await RoadmapAutomationTestSetup.ApplyPlan(
            workspace,
            plan,
            snapshot,
            TestContext.Current.CancellationToken);
        await RoadmapAutomationTestSetup.RefreshReconciliation(
            workspace,
            snapshot,
            TestContext.Current.CancellationToken);
        using var manifest = JsonDocument.Parse(workspace.ReadFile("roadmap/reconciliation/open-issues-test.json"));
        var transition = manifest.RootElement.GetProperty("closedItemTransitions").EnumerateArray().ShouldHaveSingleItem();

        // Assert
        transition.GetProperty("roadmapItem").GetString().ShouldBe("RM-018");
        transition.GetProperty("closeReason").GetString().ShouldBe("NOT_PLANNED");
    }
}
