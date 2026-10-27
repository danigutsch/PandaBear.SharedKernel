namespace PandaBear.RepoConfig.Tests;

/// <summary>Fixed values and a factory for a valid roadmap automation plan used in tests.</summary>
internal static class RoadmapAutomationTestPlan
{
    /// <summary>A 40-character commit SHA used as both the base and head commit of the test plan.</summary>
    public const string BaseSha = "1111111111111111111111111111111111111111";

    /// <summary>A 40-character commit SHA that differs from <see cref="BaseSha"/>, for head-mismatch scenarios.</summary>
    public const string DifferentSha = "2222222222222222222222222222222222222222";

    /// <summary>A 64-character digest used as the plan's input digest.</summary>
    public const string InputDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>A 64-character digest used as the plan's snapshot digest.</summary>
    public const string SnapshotDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    /// <summary>Creates a plan for <c>owner/repository</c> run 42 with one already-applied transition (RM-090) and the given required transitions.</summary>
    /// <param name="requiredTransitions">The closed-item transitions the plan requires, or <see langword="null"/> for the single transition 100 to RM-100.</param>
    /// <param name="snapshotChanged">Whether the plan records that the snapshot changed.</param>
    public static RoadmapAutomationPlan Create(
        IReadOnlyList<RoadmapClosedItemTransition>? requiredTransitions = null,
        bool snapshotChanged = false)
    {
        return RoadmapAutomationPlan.Create(
            "owner/repository",
            runId: 42,
            BaseSha,
            BaseSha,
            SnapshotDigest,
            InputDigest,
            "roadmap/reconciliation/open-issues-test.json",
            new RoadmapClosedItemTransition[] { new(90, "RM-090") },
            requiredTransitions ?? [new RoadmapClosedItemTransition(100, "RM-100")],
            snapshotChanged);
    }
}
