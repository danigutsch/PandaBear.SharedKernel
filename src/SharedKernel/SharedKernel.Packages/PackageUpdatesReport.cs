namespace SharedKernel.Packages;

/// <summary>The result of <see cref="PackageInventory.CheckUpdates"/>: update decisions per package manager.</summary>
public sealed class PackageUpdatesReport
{
    /// <summary>Creates the report.</summary>
    internal PackageUpdatesReport(IReadOnlyList<PackageManagerKind> unsupported, IReadOnlyList<MissingTool> missingTools, IReadOnlyList<ManagerUpdates> managers)
    {
        Unsupported = unsupported;
        MissingTools = missingTools;
        Managers = managers;
    }

    /// <summary>Gets the declared package managers that have no release-date source, in the order of <see cref="DeclaredPackages.Managers"/>. They are skipped.</summary>
    public IReadOnlyList<PackageManagerKind> Unsupported { get; }

    /// <summary>Gets the programs that were not found. When any is missing, nothing was run or contacted and <see cref="Managers"/> is empty.</summary>
    public IReadOnlyList<MissingTool> MissingTools { get; }

    /// <summary>Gets the results of the supported managers, in the order of <see cref="DeclaredPackages.Managers"/>; empty when there is nothing to check.</summary>
    public IReadOnlyList<ManagerUpdates> Managers { get; }

    /// <summary>Gets whether a program was missing or any manager failed; a failed report has no trustworthy summary.</summary>
    public bool HasFailure => MissingTools.Count > 0 || Managers.Any(static m => m.Error is not null);

    /// <summary>Gets whether some declared package was skipped: an unsupported manager or a package that is not on its registry.</summary>
    public bool HasSkipped => Unsupported.Count > 0 || Managers.Any(static m => m.NotOnRegistry.Count > 0);

    /// <summary>Gets how many packages were checked.</summary>
    public int CheckedCount => Managers.Sum(static m => m.CheckedCount);

    /// <summary>Gets how many checked packages have a newer stable version.</summary>
    public int UpdateCount => Managers.Sum(static m => m.Decisions.Count);
}
