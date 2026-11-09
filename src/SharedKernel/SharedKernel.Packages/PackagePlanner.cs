namespace PandaBear.Packages;

/// <summary>Compares requirements with installed packages. It reads nothing and changes nothing.</summary>
public static class PackagePlanner
{
    /// <summary>
    /// Returns the requirements that the installed packages do not meet, ordered by package name. A package that is
    /// installed and has no required version is met; an extra installed package is not a difference.
    /// </summary>
    /// <param name="requirements">What should be installed.</param>
    /// <param name="installed">What is installed.</param>
    /// <exception cref="ArgumentException">A package name appears twice in <paramref name="requirements"/> or <paramref name="installed"/>.</exception>
    public static IReadOnlyList<PackageChange> Plan(IEnumerable<PackageRequirement> requirements, IEnumerable<InstalledPackage> installed)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(installed);

        Dictionary<PackageName, PackageVersion> have = [];
        foreach (var package in installed)
        {
            if (!have.TryAdd(package.Name, package.Version))
            {
                throw new ArgumentException($"Package {package.Name} is installed twice.", nameof(installed));
            }
        }

        HashSet<PackageName> seen = [];
        List<PackageChange> changes = [];
        foreach (var requirement in requirements)
        {
            if (!seen.Add(requirement.Name))
            {
                throw new ArgumentException($"Package {requirement.Name} is required twice.", nameof(requirements));
            }

            if (!have.TryGetValue(requirement.Name, out var version))
            {
                changes.Add(new PackageChange(PackageChangeKind.Install, requirement));
            }
            else if (requirement.Version is not null && requirement.Version != version)
            {
                changes.Add(new PackageChange(PackageChangeKind.ChangeVersion, requirement, version));
            }
        }

        return [.. changes.OrderBy(static change => change.Requirement.Name.Value, StringComparer.Ordinal)];
    }
}
