namespace SharedKernel.Versioning;

/// <summary>
/// Defines inputs for release-preparation artifact generation.
/// </summary>
/// <param name="Version">The release version.</param>
/// <param name="PackageDirectory">The directory containing package artifacts.</param>
/// <param name="OutputDirectory">The directory that receives release-preparation artifacts.</param>
/// <param name="SourceTag">The previous release tag, when one exists.</param>
/// <param name="ReleaseImpact">The release impact text.</param>
/// <param name="Sha">The source commit SHA.</param>
public sealed record ReleasePreparationOptions(
    string Version,
    string PackageDirectory,
    string OutputDirectory,
    string? SourceTag,
    string? ReleaseImpact,
    string? Sha);
