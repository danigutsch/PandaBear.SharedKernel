namespace SharedKernel.RepoConfig.Tests;

internal static class RoadmapAutomationTestPlan
{
    public const string BaseSha = "1111111111111111111111111111111111111111";
    public const string DifferentSha = "2222222222222222222222222222222222222222";
    public const string InputDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public const string SnapshotDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

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
            new Dictionary<int, string> { [90] = "RM-090" },
            requiredTransitions ?? [new RoadmapClosedItemTransition(100, "RM-100")],
            snapshotChanged);
    }
}
