namespace SharedKernel.RepoConfig.Tool;

internal static class RoadmapAutomationPlanValidator
{
    public static void ValidateBeforeWrite(
        RoadmapAutomationPlan plan,
        string expectedPlanDigest,
        string expectedRepository,
        string currentHead,
        string currentDefaultBranchOid,
        string currentSnapshotDigest,
        string currentInputDigest,
        IReadOnlyDictionary<int, string> currentBaselineTransitions,
        IReadOnlyList<RoadmapClosedItemTransition> currentRequiredTransitions)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentBaselineTransitions);
        ArgumentNullException.ThrowIfNull(currentRequiredTransitions);

        if (!string.Equals(plan.PlanDigest, expectedPlanDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan digest changed after approval.");
        }

        if (!string.Equals(plan.Repository, expectedRepository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Roadmap automation plan repository does not match the workflow repository.");
        }

        if (!string.Equals(plan.BaseSha, currentHead, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan base SHA is stale.");
        }

        if (!string.Equals(plan.DefaultBranchOid, currentDefaultBranchOid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan default-branch OID is stale.");
        }

        if (!string.Equals(plan.SnapshotDigest, currentSnapshotDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation GitHub snapshot is stale.");
        }

        if (!string.Equals(plan.RepositoryInputDigest, currentInputDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation repository input snapshot is stale.");
        }

        var baselineDigest = RoadmapAutomationPlan.ComputeTransitionsDigest(currentBaselineTransitions);
        if (!string.Equals(plan.BaselineClosedTransitionsDigest, baselineDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation baseline closure approvals changed after planning.");
        }

        var required = currentRequiredTransitions.OrderBy(transition => transition.Issue).ToArray();
        if (!plan.RequiredClosedTransitions.SequenceEqual(required))
        {
            throw new InvalidOperationException("Roadmap automation closure candidates changed after approval.");
        }

        var approved = currentBaselineTransitions
            .Select(transition => new RoadmapClosedItemTransition(transition.Key, transition.Value))
            .Concat(required)
            .OrderBy(transition => transition.Issue)
            .ToArray();
        var approvedDigest = RoadmapAutomationPlan.ComputeTransitionsDigest(approved);
        if (!string.Equals(plan.ApprovedClosedTransitionsDigest, approvedDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation approved closure digest changed after planning.");
        }

        var hasChanges = plan.SnapshotChanged || required.Length > 0;
        if (plan.HasChanges != hasChanges)
        {
            throw new InvalidOperationException("Roadmap automation plan change metadata is inconsistent.");
        }
    }

    public static void ValidateBeforePush(
        RoadmapAutomationPlan plan,
        string expectedPlanDigest,
        string expectedRepository,
        string currentHead,
        string currentDefaultBranchOid,
        string currentSnapshotDigest,
        IReadOnlyDictionary<int, string> currentApprovedTransitions,
        IReadOnlyList<RoadmapClosedItemTransition> currentRequiredTransitions)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(currentApprovedTransitions);
        ArgumentNullException.ThrowIfNull(currentRequiredTransitions);

        if (!string.Equals(plan.PlanDigest, expectedPlanDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan digest changed after approval.");
        }

        if (!string.Equals(plan.Repository, expectedRepository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Roadmap automation plan repository does not match the workflow repository.");
        }

        if (!string.Equals(plan.BaseSha, currentHead, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan base SHA is stale.");
        }

        if (!string.Equals(plan.DefaultBranchOid, currentDefaultBranchOid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan default-branch OID is stale.");
        }

        if (!string.Equals(plan.SnapshotDigest, currentSnapshotDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation GitHub snapshot is stale.");
        }

        if (currentRequiredTransitions.Count > 0)
        {
            throw new InvalidOperationException("Roadmap automation closure candidates changed after the protected apply.");
        }

        var approvedDigest = RoadmapAutomationPlan.ComputeTransitionsDigest(currentApprovedTransitions);
        if (!string.Equals(plan.ApprovedClosedTransitionsDigest, approvedDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation closure approvals changed after the protected apply.");
        }
    }
}
