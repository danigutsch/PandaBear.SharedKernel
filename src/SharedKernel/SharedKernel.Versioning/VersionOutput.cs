namespace SharedKernel.Versioning;

/// <summary>
/// Represents release version values consumed by package, assembly, and automation flows.
/// </summary>
/// <param name="SemVer">The calculated semantic version.</param>
/// <param name="ReleaseImpact">The highest release impact.</param>
/// <param name="PackageVersion">The NuGet package version.</param>
/// <param name="AssemblyVersion">The assembly identity version.</param>
/// <param name="FileVersion">The file version.</param>
/// <param name="InformationalVersion">The informational version.</param>
public sealed record VersionOutput(
    string SemVer,
    ReleaseImpact ReleaseImpact,
    string PackageVersion,
    string AssemblyVersion,
    string FileVersion,
    string InformationalVersion)
{
    /// <summary>
    /// Creates version output values from a semantic version.
    /// </summary>
    /// <param name="version">The semantic version.</param>
    /// <param name="impact">The release impact.</param>
    /// <param name="sha">The optional source SHA.</param>
    /// <returns>The version output.</returns>
    public static VersionOutput Create(SemanticVersion version, ReleaseImpact impact, string? sha = null)
    {
        ArgumentNullException.ThrowIfNull(version);

        var semanticVersion = version.ToString();
        var informationalVersion = string.IsNullOrWhiteSpace(sha)
            ? version
            : version with { BuildMetadata = AppendBuildMetadata(version.BuildMetadata, $"sha.{sha}") };

        return new VersionOutput(
            semanticVersion,
            impact,
            semanticVersion,
            $"{version.Major}.0.0.0",
            $"{version.Major}.{version.Minor}.{version.Patch}.0",
            informationalVersion.ToString());
    }

    private static string AppendBuildMetadata(string? existingMetadata, string newMetadata) =>
        string.IsNullOrWhiteSpace(existingMetadata) ? newMetadata : $"{existingMetadata}.{newMetadata}";
}
