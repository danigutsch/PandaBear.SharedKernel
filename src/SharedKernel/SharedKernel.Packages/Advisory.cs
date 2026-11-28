using SharedKernel.Domain;
using SharedKernel.Results;

namespace SharedKernel.Packages;

/// <summary>
/// A security advisory for one package with exact affected versions and optional known SemVer intervals.
/// </summary>
public sealed class Advisory : ValueObject
{
    /// <summary>The longest allowed advisory identifier.</summary>
    public const int MaxIdLength = 128;

    private Advisory(
        string id, PackageName package, AdvisorySeverity severity, PackageVersion[] affectedVersions, AdvisoryVersionRange[] affectedRanges)
    {
        Id = id;
        Package = package;
        Severity = severity;
        AffectedVersions = Array.AsReadOnly(affectedVersions);
        AffectedRanges = Array.AsReadOnly(affectedRanges);
    }

    /// <summary>Gets the advisory identifier, such as <c>GHSA-xxxx-xxxx-xxxx</c>.</summary>
    public string Id { get; }

    /// <summary>Gets the affected package.</summary>
    public PackageName Package { get; }

    /// <summary>Gets how serious the advisory is.</summary>
    public AdvisorySeverity Severity { get; }

    /// <summary>Gets the affected versions, ordered by text.</summary>
    public IReadOnlyList<PackageVersion> AffectedVersions { get; }

    /// <summary>Gets any known affected SemVer intervals and their explicit fixes.</summary>
    public IReadOnlyList<AdvisoryVersionRange> AffectedRanges { get; }

    /// <summary>Creates an advisory.</summary>
    /// <param name="id">The identifier: 1 to 128 printable ASCII characters without spaces.</param>
    /// <param name="package">The affected package.</param>
    /// <param name="severity">How serious the advisory is.</param>
    /// <param name="affectedVersions">The affected versions; at least one, duplicates are merged.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The advisory or a validation error that never echoes the identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="package"/> or <paramref name="affectedVersions"/> is <see langword="null"/>.</exception>
    public static Result<Advisory> Create(string? id, PackageName package, AdvisorySeverity severity, IEnumerable<PackageVersion> affectedVersions, string field = "advisory") =>
        Create(id, package, severity, affectedVersions, [], field, "An advisory must list at least one affected version.");

    /// <summary>Creates an advisory containing at least one exact affected version or known SemVer interval.</summary>
    /// <param name="id">The advisory identifier.</param>
    /// <param name="package">The affected package.</param>
    /// <param name="severity">The advisory severity.</param>
    /// <param name="affectedVersions">The exact affected versions.</param>
    /// <param name="affectedRanges">The known affected SemVer intervals.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The advisory or a validation error.</returns>
    /// <exception cref="ArgumentNullException">A package or a collection is <see langword="null"/>.</exception>
    public static Result<Advisory> CreateWithRanges(
        string? id,
        PackageName package,
        AdvisorySeverity severity,
        IEnumerable<PackageVersion> affectedVersions,
        IEnumerable<AdvisoryVersionRange> affectedRanges,
        string field = "advisory") =>
        Create(id, package, severity, affectedVersions, affectedRanges, field, "An advisory must list at least one affected version or range.");

    private static Result<Advisory> Create(
        string? id,
        PackageName package,
        AdvisorySeverity severity,
        IEnumerable<PackageVersion> affectedVersions,
        IEnumerable<AdvisoryVersionRange> affectedRanges,
        string field,
        string missingAffectedVersions)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(affectedVersions);
        ArgumentNullException.ThrowIfNull(affectedRanges);

        var versions = affectedVersions.Distinct().OrderBy(static v => v.Value, StringComparer.Ordinal).ToArray();
        var ranges = affectedRanges.Distinct()
            .OrderBy(static range => range.IntroducedVersion?.Value, StringComparer.Ordinal)
            .ThenBy(static range => range.FixedVersion?.Value, StringComparer.Ordinal)
            .ToArray();
        string? problem = id switch
        {
            null or "" => "Advisory identifier is required.",
            { Length: > MaxIdLength } => $"Advisory identifier cannot exceed {MaxIdLength} characters.",
            _ when !id.All(static c => c is > ' ' and < '\u007f') => "Advisory identifier can only contain printable ASCII characters without spaces.",
            _ when !Enum.IsDefined(severity) => "Advisory severity is not a known value.",
            _ when versions.Length == 0 && ranges.Length == 0 => missingAffectedVersions,
            _ => null,
        };
        return problem is null ? Result.Ok(new Advisory(id!, package, severity, versions, ranges)) : Result.Invalid<Advisory>(problem, field, problem);
    }

    /// <summary>Returns whether <paramref name="version"/> is listed or covered by a known SemVer interval.</summary>
    /// <param name="version">The version to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    public bool Affects(PackageVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return AffectedVersions.Contains(version) || AffectedRanges.Any(range => range.Contains(version));
    }

    /// <inheritdoc />
    public override string ToString() => $"{Id} ({Severity}) {Package}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return Package;
        yield return Severity;
        foreach (var version in AffectedVersions)
        {
            yield return version;
        }

        foreach (var range in AffectedRanges)
        {
            yield return range;
        }
    }
}
