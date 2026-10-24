namespace PandaBear.RepoConfig.Tests;

/// <summary>Describes the pinned candidate and the locked packages of a license exception test repository.</summary>
internal sealed record LicenseScenario(
    string CandidateVersion = LicenseExceptionTestRepository.ApprovedVersion,
    string DependencyVersion = "2.25.29",
    string MagickVersion = "15.0.0",
    bool Dirty = false,
    bool CorrectRevision = true,
    bool ConflictingTarget = false,
    string PackageName = "StreamJsonRpc");
