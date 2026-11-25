namespace SharedKernel.Packages;

/// <summary>The plan for the packages that one package manager owns, or why its installed packages could not be read.</summary>
public sealed class ManagerPlan
{
    /// <summary>Creates the plan of one manager.</summary>
    internal ManagerPlan(PackageManagerKind manager, int declaredCount, IReadOnlyList<PackageChange> changes, string? error)
    {
        Manager = manager;
        DeclaredCount = declaredCount;
        Changes = changes;
        Error = error;
    }

    /// <summary>Gets the package manager.</summary>
    public PackageManagerKind Manager { get; }

    /// <summary>Gets how many packages are declared for the manager.</summary>
    public int DeclaredCount { get; }

    /// <summary>Gets the changes that the declaration needs, ordered by package name; empty when <see cref="Error"/> is set.</summary>
    public IReadOnlyList<PackageChange> Changes { get; }

    /// <summary>
    /// Gets why the installed packages could not be read, or <see langword="null"/>. The text can carry the tool's own
    /// output, so escape it before printing.
    /// </summary>
    public string? Error { get; }
}
