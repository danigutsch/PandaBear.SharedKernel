namespace SharedKernel.Packages;

/// <summary>The update decisions for the installed packages that one package manager owns, or why they could not be made.</summary>
public sealed class ManagerUpdates
{
    /// <summary>Creates the result of one manager.</summary>
    internal ManagerUpdates(
        PackageManagerKind manager,
        IReadOnlyList<PackageRequirement> pinned,
        IReadOnlyList<PackageName> notOnRegistry,
        IReadOnlyList<UpdateDecision> decisions,
        int checkedCount,
        string? error)
    {
        Manager = manager;
        Pinned = pinned;
        NotOnRegistry = notOnRegistry;
        Decisions = decisions;
        CheckedCount = checkedCount;
        Error = error;
    }

    /// <summary>Gets the package manager.</summary>
    public PackageManagerKind Manager { get; }

    /// <summary>Gets the installed packages that are declared at an exact version, in the order the tool lists them. They are not checked.</summary>
    public IReadOnlyList<PackageRequirement> Pinned { get; }

    /// <summary>Gets the packages that the registry does not know, ordered by name. They are not checked, so the result is incomplete.</summary>
    public IReadOnlyList<PackageName> NotOnRegistry { get; }

    /// <summary>Gets one decision per package whose newest stable version differs from the installed one; empty when <see cref="Error"/> is set.</summary>
    public IReadOnlyList<UpdateDecision> Decisions { get; }

    /// <summary>Gets how many packages were checked against the registry; 0 when <see cref="Error"/> is set.</summary>
    public int CheckedCount { get; }

    /// <summary>
    /// Gets the first failure (reading the installed packages or a lookup), or <see langword="null"/>. No decision is
    /// returned for a manager with an error. The text can carry tool or registry output, so escape it before printing.
    /// </summary>
    public string? Error { get; }
}
