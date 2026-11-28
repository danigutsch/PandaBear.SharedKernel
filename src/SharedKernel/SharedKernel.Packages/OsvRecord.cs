namespace SharedKernel.Packages;

/// <summary>What the adapter keeps of one OSV vulnerability record.</summary>
/// <param name="Withdrawn">Whether the database withdrew the record.</param>
/// <param name="Severity">The mapped severity.</param>
/// <param name="VersionsByPackage">The <c>affected[].versions</c> lists, keyed by ecosystem, a line feed, and the package name.</param>
/// <param name="RangesByPackage">The supported SemVer intervals, keyed in the same way as <paramref name="VersionsByPackage"/>.</param>
internal sealed record OsvRecord(
    bool Withdrawn,
    AdvisorySeverity Severity,
    IReadOnlyDictionary<string, string[]> VersionsByPackage,
    IReadOnlyDictionary<string, AdvisoryVersionRange[]> RangesByPackage);
