using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Packages;

/// <summary>An affected SemVer interval with an inclusive introduction and an exclusive, explicitly fixed upper bound.</summary>
public sealed class AdvisoryVersionRange : ValueObject
{
    private AdvisoryVersionRange(PackageVersion? introducedVersion, PackageVersion? fixedVersion)
    {
        IntroducedVersion = introducedVersion;
        FixedVersion = fixedVersion;
    }

    /// <summary>Gets the first affected version, or <see langword="null"/> for an interval open below.</summary>
    public PackageVersion? IntroducedVersion { get; }

    /// <summary>Gets the first fixed version, or <see langword="null"/> when no fix is known.</summary>
    public PackageVersion? FixedVersion { get; }

    /// <summary>Creates an interval whose specified bounds are SemVer values in ascending order.</summary>
    /// <param name="introducedVersion">The first affected version, if specified.</param>
    /// <param name="fixedVersion">The first version containing the fix, if specified.</param>
    /// <returns>The affected interval or a validation error for unsupported or unordered bounds.</returns>
    public static Result<AdvisoryVersionRange> Create(PackageVersion? introducedVersion, PackageVersion? fixedVersion)
    {
        if ((introducedVersion is not null && PackageVersionPrecedence.CompareSemVer(introducedVersion, introducedVersion) is null)
            || (fixedVersion is not null && PackageVersionPrecedence.CompareSemVer(fixedVersion, fixedVersion) is null)
            || (introducedVersion is not null && fixedVersion is not null
                && PackageVersionPrecedence.CompareSemVer(introducedVersion, fixedVersion) is not < 0))
        {
            const string message = "Advisory range bounds must be semantic versions with the fixed version strictly after the introduction.";
            return Result.Invalid<AdvisoryVersionRange>(message, "range", message);
        }

        return Result.Ok(new AdvisoryVersionRange(introducedVersion, fixedVersion));
    }

    /// <summary>Reports whether the known SemVer interval covers the version.</summary>
    internal bool Contains(PackageVersion version)
    {
        var lowerOrder = PackageVersionPrecedence.CompareSemVer(version, IntroducedVersion ?? version);
        var upperOrder = FixedVersion is null ? -1 : PackageVersionPrecedence.CompareSemVer(version, FixedVersion);
        return lowerOrder is >= 0 && upperOrder is < 0;
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IntroducedVersion;
        yield return FixedVersion;
    }
}
