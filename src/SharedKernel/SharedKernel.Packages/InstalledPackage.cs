using PandaBear.BuildingBlocks;

namespace PandaBear.Packages;

/// <summary>A package that a package manager reports as installed.</summary>
public sealed class InstalledPackage : ValueObject
{
    /// <summary>Creates an installed package.</summary>
    /// <param name="name">The package name.</param>
    /// <param name="version">The installed version.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    public InstalledPackage(PackageName name, PackageVersion version)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(version);
        Name = name;
        Version = version;
    }

    /// <summary>Gets the package name.</summary>
    public PackageName Name
    {
        get;
    }

    /// <summary>Gets the installed version.</summary>
    public PackageVersion Version
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name}={Version}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Version;
    }
}
