using PandaBear.BuildingBlocks;

namespace PandaBear.Packages;

/// <summary>A package that should be installed, optionally at one exact version.</summary>
public sealed class PackageRequirement : ValueObject
{
    /// <summary>Creates a requirement.</summary>
    /// <param name="name">The package name.</param>
    /// <param name="version">The exact version that is required, or <see langword="null"/> when any version will do.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public PackageRequirement(PackageName name, PackageVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Version = version;
    }

    /// <summary>Gets the package name.</summary>
    public PackageName Name
    {
        get;
    }

    /// <summary>Gets the exact version that is required, or <see langword="null"/> when any version will do.</summary>
    public PackageVersion? Version
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString() => Version is null ? Name.Value : $"{Name}={Version}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Version;
    }
}
