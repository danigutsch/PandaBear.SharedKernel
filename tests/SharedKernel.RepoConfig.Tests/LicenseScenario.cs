namespace PandaBear.RepoConfig.Tests;

/// <summary>Describes the pinned candidate and the locked packages of a license exception test repository.</summary>
/// <param name="CandidateVersion">The package version written to the candidate pin.</param>
/// <param name="DependencyVersion">The locked version of the primary exception package, or empty to lock a different package instead.</param>
/// <param name="MagickVersion">The locked version of the image package, or empty to lock a different package instead.</param>
/// <param name="Dirty">Whether the candidate pin reports a dirty library tree.</param>
/// <param name="CorrectRevision">Whether the candidate pin records the approved library revision.</param>
/// <param name="ConflictingTarget">Whether the <c>net10.0/win-x64</c> target locks a different version of the primary package.</param>
/// <param name="PackageName">The name of the primary exception package.</param>
internal sealed record LicenseScenario(
    string CandidateVersion = LicenseExceptionTestRepository.ApprovedVersion,
    string DependencyVersion = "2.25.29",
    string MagickVersion = "15.0.0",
    bool Dirty = false,
    bool CorrectRevision = true,
    bool ConflictingTarget = false,
    string PackageName = "StreamJsonRpc");
