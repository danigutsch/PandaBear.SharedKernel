namespace PandaBear.RepoConfig.Tool;

/// <summary>Checks that a roadmap automation plan is still current before result files are written or pushed.</summary>
internal static class RoadmapAutomationPlanValidator
{
    /// <summary>Throws unless the plan digest, repository, default-branch head, GitHub snapshot, input snapshot, and closed transitions all still match the plan.</summary>
    public static void ValidateBeforeWrite(
        RoadmapAutomationPlan plan,
        string expectedPlanDigest,
        string expectedRepository,
        string currentHead,
        string currentDefaultBranchOid,
        string currentSnapshotDigest,
        string currentInputDigest,
        IReadOnlyList<RoadmapClosedItemTransition> currentBaselineTransitions,
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

        RequireCurrentDefaultBranchHead(currentHead, currentDefaultBranchOid);

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

    /// <summary>Throws unless the plan digest, repository, default-branch head, GitHub snapshot, and approved and required transitions still match the plan just before push.</summary>
    public static void ValidateBeforePush(
        RoadmapAutomationPlan plan,
        string expectedPlanDigest,
        string expectedRepository,
        string currentHead,
        string currentDefaultBranchOid,
        string currentSnapshotDigest,
        IReadOnlyList<RoadmapClosedItemTransition> currentApprovedTransitions,
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

        RequireCurrentDefaultBranchHead(currentHead, currentDefaultBranchOid);

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

    /// <summary>
    /// The plan is bound to its repository inputs, not to commit SHAs. A newer default-branch head is
    /// accepted as long as the checkout is that head; the input digest rejects any roadmap change.
    /// </summary>
    private static void RequireCurrentDefaultBranchHead(string currentHead, string currentDefaultBranchOid)
    {
        if (!string.Equals(currentHead, currentDefaultBranchOid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation checkout is not the current default-branch head.");
        }
    }
}
