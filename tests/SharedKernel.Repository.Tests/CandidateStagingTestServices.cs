using PandaBear.CI.Tool;

namespace PandaBear.Repository.Tests;

/// <summary>Builds stage-candidate services from test doubles, so no test reaches Git or the network.</summary>
internal static class CandidateStagingTestServices
{
    /// <summary>Gets small limits, so size tests stay small: the scope's two 4096-byte packages fit them.</summary>
    public static CandidateDownloadLimits SmallLimits
    {
        get;
    } = CandidateDownloadLimits.Default with
    {
        MaxPackageBytes = 6000,
        MaxTotalBytes = 20000,
    };

    /// <summary>Uses <paramref name="git"/>, a release server without responses, and the system clock.</summary>
    public static CandidateStagingServices Offline(CandidateGit git)
    {
        ArgumentNullException.ThrowIfNull(git);
        return new CandidateStagingServices(git.Run, static () => new FakeReleaseServer(), TimeProvider.System, CandidateDownloadLimits.Default);
    }

    /// <summary>Uses <paramref name="git"/> and <paramref name="server"/>, with an optional clock and download limits that default to the system clock and the default limits.</summary>
    public static CandidateStagingServices Create(
        CandidateGit git,
        FakeReleaseServer server,
        TimeProvider? timeProvider = null,
        CandidateDownloadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(git);
        return new CandidateStagingServices(git.Run, () => server, timeProvider ?? TimeProvider.System, limits ?? CandidateDownloadLimits.Default);
    }
}
