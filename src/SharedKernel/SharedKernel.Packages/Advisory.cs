using PandaBear.BuildingBlocks;
using PandaBear.Results;

namespace PandaBear.Packages;

/// <summary>
/// A security advisory for one package. It lists the exact versions that are affected, because version order is specific
/// to each package manager and this library never compares versions.
/// </summary>
public sealed class Advisory : ValueObject
{
    /// <summary>The longest allowed advisory identifier.</summary>
    public const int MaxIdLength = 128;

    private readonly PackageVersion[] _affectedVersions;

    private Advisory(string id, PackageName package, AdvisorySeverity severity, PackageVersion[] affectedVersions)
    {
        Id = id;
        Package = package;
        Severity = severity;
        _affectedVersions = affectedVersions;
    }

    /// <summary>Gets the advisory identifier, such as <c>GHSA-xxxx-xxxx-xxxx</c>.</summary>
    public string Id
    {
        get;
    }

    /// <summary>Gets the affected package.</summary>
    public PackageName Package
    {
        get;
    }

    /// <summary>Gets how serious the advisory is.</summary>
    public AdvisorySeverity Severity
    {
        get;
    }

    /// <summary>Gets the affected versions, ordered by text.</summary>
    public IReadOnlyList<PackageVersion> AffectedVersions => _affectedVersions;

    /// <summary>Creates an advisory.</summary>
    /// <param name="id">The identifier: 1 to 128 printable ASCII characters without spaces.</param>
    /// <param name="package">The affected package.</param>
    /// <param name="severity">How serious the advisory is.</param>
    /// <param name="affectedVersions">The affected versions; at least one, duplicates are merged.</param>
    /// <param name="field">The field name used in validation errors.</param>
    /// <returns>The advisory or a validation error that never echoes the identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="package"/> or <paramref name="affectedVersions"/> is <see langword="null"/>.</exception>
    public static Result<Advisory> Create(string? id, PackageName package, AdvisorySeverity severity, IEnumerable<PackageVersion> affectedVersions, string field = "advisory")
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(affectedVersions);

        var versions = affectedVersions.Distinct().OrderBy(static v => v.Value, StringComparer.Ordinal).ToArray();
        string? problem = id switch
        {
            null or "" => "Advisory identifier is required.",
            { Length: > MaxIdLength } => $"Advisory identifier cannot exceed {MaxIdLength} characters.",
            _ when !id.All(static c => c is > ' ' and < '\u007f') => "Advisory identifier can only contain printable ASCII characters without spaces.",
            _ when !Enum.IsDefined(severity) => "Advisory severity is not a known value.",
            _ when versions.Length == 0 => "An advisory must list at least one affected version.",
            _ => null,
        };
        return problem is null ? Result.Ok(new Advisory(id!, package, severity, versions)) : Result.Invalid<Advisory>(problem, field, problem);
    }

    /// <summary>Returns whether <paramref name="version"/> is one of the affected versions.</summary>
    /// <param name="version">The version to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    public bool Affects(PackageVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Array.IndexOf(_affectedVersions, version) >= 0;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Id} ({Severity}) {Package}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
        yield return Package;
        yield return Severity;
        foreach (var version in _affectedVersions)
        {
            yield return version;
        }
    }
}
