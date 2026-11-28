namespace SharedKernel.Packages;

/// <summary>The observed plan, any attempted changes, and the resulting installed-state check.</summary>
public sealed class PackageConvergenceReport
{
    /// <summary>Creates a convergence report.</summary>
    internal PackageConvergenceReport(
        PackagePlanReport plan, bool applyRequested, IReadOnlyList<PackageChangeExecution> changes, PackagePlanReport? remaining)
    {
        Plan = plan;
        ApplyRequested = applyRequested;
        Changes = Array.AsReadOnly(changes.ToArray());
        Remaining = remaining;
    }

    /// <summary>Gets the plan made from the initial observed state.</summary>
    public PackagePlanReport Plan { get; }

    /// <summary>Gets whether the caller explicitly requested application.</summary>
    public bool ApplyRequested { get; }

    /// <summary>Gets the audited outcomes in plan order.</summary>
    public IReadOnlyList<PackageChangeExecution> Changes { get; }

    /// <summary>Gets the remaining plan after application, when the initial plan could be applied.</summary>
    public PackagePlanReport? Remaining { get; }

    /// <summary>Gets whether preview completed or an apply run succeeded and verified the desired installed state.</summary>
    public bool IsComplete => Plan.IsComplete && (!ApplyRequested
        || (Changes.All(static change => change.Outcome == PackageChangeOutcome.Applied)
            && Remaining is { IsComplete: true, ChangeCount: 0 }));
}
