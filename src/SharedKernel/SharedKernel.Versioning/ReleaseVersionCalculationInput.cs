namespace SharedKernel.Versioning;

/// <summary>
/// Defines inputs for release version calculation from Git history output.
/// </summary>
/// <param name="SourceSha">The source commit SHA.</param>
/// <param name="SourceTag">The latest release tag, or an empty value when none exists.</param>
/// <param name="LogOutput">Null-separated commit messages.</param>
/// <param name="VersionKind">The version kind, either <c>prerelease</c> or <c>stable</c>.</param>
/// <param name="RunNumber">The CI run number used in prerelease labels.</param>
public sealed record ReleaseVersionCalculationInput(
    string SourceSha,
    string SourceTag,
    string LogOutput,
    string VersionKind,
    string? RunNumber);
