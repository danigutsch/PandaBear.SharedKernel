using PandaBear.BuildingBlocks;

namespace PandaBear.Packages;

/// <summary>An installed package and the different version that its registry offers, before the release time is known.</summary>
public sealed class PackageUpdate : ValueObject
{
    /// <summary>Creates an update.</summary>
    /// <param name="package">The package.</param>
    /// <param name="installedVersion">The installed version.</param>
    /// <param name="candidateVersion">The version that could be installed.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The candidate version equals the installed version.</exception>
    public PackageUpdate(PackageName package, PackageVersion installedVersion, PackageVersion candidateVersion)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(installedVersion);
        ArgumentNullException.ThrowIfNull(candidateVersion);
        if (installedVersion == candidateVersion)
        {
            throw new ArgumentException("The candidate version must differ from the installed version.", nameof(candidateVersion));
        }

        Package = package;
        InstalledVersion = installedVersion;
        CandidateVersion = candidateVersion;
    }

    /// <summary>Gets the package.</summary>
    public PackageName Package
    {
        get;
    }

    /// <summary>Gets the installed version.</summary>
    public PackageVersion InstalledVersion
    {
        get;
    }

    /// <summary>Gets the version that could be installed.</summary>
    public PackageVersion CandidateVersion
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Package} {InstalledVersion} -> {CandidateVersion}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Package;
        yield return InstalledVersion;
        yield return CandidateVersion;
    }
}
