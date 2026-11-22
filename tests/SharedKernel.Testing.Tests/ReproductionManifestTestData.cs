using PandaBear.Testing.Snapshots;

namespace PandaBear.Testing.Tests;

/// <summary>Supplies approved metadata for reproduction manifest tests.</summary>
internal static class ReproductionManifestTestData
{
    private const string Revision = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";

    /// <summary>Creates a fictional approved attempt and its specification.</summary>
    internal static (ReproductionSpecification Specification, ReproductionManifest Manifest) Create()
    {
        const string scenario = "venue.fixture";
        var outcome = new ReproductionOutcome("completed", "confirmed", new Dictionary<string, string>(StringComparer.Ordinal));
        return (new ReproductionSpecification(scenario, Revision, [outcome]),
            new ReproductionManifest(Revision, scenario, Guid.NewGuid(), [], outcome, DateTimeOffset.UnixEpoch, 25));
    }
}
