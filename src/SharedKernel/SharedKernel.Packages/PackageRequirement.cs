using SharedKernel.Domain;

namespace SharedKernel.Packages;

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

    private PackageRequirement(PackageName name, PackagePin pin) : this(name, pin.Version) => Pin = pin;

    /// <summary>Gets the package name.</summary>
    public PackageName Name { get; }

    /// <summary>Gets the exact version that is required, or <see langword="null"/> when any version will do.</summary>
    public PackageVersion? Version { get; }

    /// <summary>Gets the expected artifact pin when a caller supplies a checksum.</summary>
    public PackagePin? Pin { get; }

    /// <summary>Creates a requirement for the exact version and checksum in <paramref name="pin"/>.</summary>
    /// <param name="name">The package name.</param>
    /// <param name="pin">The expected artifact pin.</param>
    /// <exception cref="ArgumentNullException">The name or pin is <see langword="null"/>.</exception>
    public static PackageRequirement Pinned(PackageName name, PackagePin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        return new PackageRequirement(name, pin);
    }

    /// <inheritdoc />
    public override string ToString() => Version is null ? Name.Value : $"{Name}={Version}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Version;
        yield return Pin;
    }
}
