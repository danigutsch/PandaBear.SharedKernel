using PandaBear.BuildingBlocks;

namespace PandaBear.Packages;

/// <summary>One difference between a requirement and the installed packages.</summary>
public sealed class PackageChange : ValueObject
{
    /// <summary>Creates a change.</summary>
    /// <param name="kind">How the installed state differs.</param>
    /// <param name="requirement">The requirement that is not met.</param>
    /// <param name="installedVersion">The installed version for <see cref="PackageChangeKind.ChangeVersion"/>; otherwise <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="requirement"/> is <see langword="null"/>.</exception>
    public PackageChange(PackageChangeKind kind, PackageRequirement requirement, PackageVersion? installedVersion = null)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        Kind = kind;
        Requirement = requirement;
        InstalledVersion = installedVersion;
    }

    /// <summary>Gets how the installed state differs.</summary>
    public PackageChangeKind Kind
    {
        get;
    }

    /// <summary>Gets the requirement that is not met.</summary>
    public PackageRequirement Requirement
    {
        get;
    }

    /// <summary>Gets the installed version for <see cref="PackageChangeKind.ChangeVersion"/>; otherwise <see langword="null"/>.</summary>
    public PackageVersion? InstalledVersion
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString() => InstalledVersion is null ? $"{Kind} {Requirement}" : $"{Kind} {Requirement} (installed {InstalledVersion})";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return Requirement;
        yield return InstalledVersion;
    }
}
