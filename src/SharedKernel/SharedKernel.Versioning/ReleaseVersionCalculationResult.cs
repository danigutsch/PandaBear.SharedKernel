namespace SharedKernel.Versioning;

/// <summary>
/// Represents release version calculation output plus automation metadata.
/// </summary>
/// <param name="BaseVersion">The base version used for calculation.</param>
/// <param name="SourceTag">The source tag used for calculation.</param>
/// <param name="VersionJson">The serialized version output.</param>
/// <param name="VersionOutput">The calculated version output.</param>
public sealed record ReleaseVersionCalculationResult(
    string BaseVersion,
    string SourceTag,
    string VersionJson,
    VersionOutput VersionOutput);
