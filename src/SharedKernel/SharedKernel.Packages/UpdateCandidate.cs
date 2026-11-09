using PandaBear.BuildingBlocks;

namespace PandaBear.Packages;

/// <summary>A newer release of an installed package, with the time it was released.</summary>
public sealed class UpdateCandidate : ValueObject
{
    /// <summary>Creates a candidate.</summary>
    /// <param name="package">The package.</param>
    /// <param name="installedVersion">The installed version.</param>
    /// <param name="candidateVersion">The version that could be installed.</param>
    /// <param name="releasedAt">When <paramref name="candidateVersion"/> was released.</param>
    /// <exception cref="ArgumentNullException">A package or version is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The candidate version equals the installed version.</exception>
    public UpdateCandidate(PackageName package, PackageVersion installedVersion, PackageVersion candidateVersion, DateTimeOffset releasedAt)
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
        ReleasedAt = releasedAt;
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

    /// <summary>Gets when the candidate version was released.</summary>
    public DateTimeOffset ReleasedAt
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
        yield return ReleasedAt;
    }
}
