namespace SharedKernel.Packages;

/// <summary>The result of <see cref="PackageInventory.Plan"/>: what the declared packages need, per package manager.</summary>
public sealed class PackagePlanReport
{
    /// <summary>Creates the report.</summary>
    internal PackagePlanReport(IReadOnlyList<MissingTool> missingTools, IReadOnlyList<ManagerPlan> managers)
    {
        MissingTools = missingTools;
        Managers = managers;
    }

    /// <summary>Gets the programs that were not found. When any is missing, nothing was run and <see cref="Managers"/> is empty.</summary>
    public IReadOnlyList<MissingTool> MissingTools { get; }

    /// <summary>Gets one plan per declared package manager, in the order of <see cref="DeclaredPackages.Managers"/>.</summary>
    public IReadOnlyList<ManagerPlan> Managers { get; }

    /// <summary>Gets whether every program was found and every installed list was read, so the changes are the whole answer.</summary>
    public bool IsComplete => MissingTools.Count == 0 && Managers.All(static m => m.Error is null);

    /// <summary>Gets how many packages are declared for the managers in <see cref="Managers"/>.</summary>
    public int DeclaredCount => Managers.Sum(static m => m.DeclaredCount);

    /// <summary>Gets how many declared packages need a change.</summary>
    public int ChangeCount => Managers.Sum(static m => m.Changes.Count);
}
