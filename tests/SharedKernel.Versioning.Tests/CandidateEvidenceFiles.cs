using System.Text.Json.Nodes;
using PandaBear.Tooling;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Writes candidate evidence manifests with fixed library revision, package version, and pending consumption status for tests.
/// </summary>
internal static class CandidateEvidenceFiles
{
    private const string Revision = "753f94c9a8dfd09084cb3b0e6d49739c0a16cac1";

    /// <summary>
    /// Writes a manifest to a file in the directory, applies the extra key and value pairs on top of the defaults, and returns the file path.
    /// </summary>
    public static string Write(string directory, string name, params (string Key, string Value)[] platform)
    {
        var manifest = new JsonObject
        {
            ["libraryRevision"] = Revision,
            ["libraryDirty"] = false,
            ["packageVersion"] = "0.1.0-alpha.171.1",
            ["packageSha256"] = new JsonObject { ["PandaBear.SharedKernel.Probe.0.1.0-alpha.171.1.nupkg"] = "abc" },
            ["consumerCompatibility"] = "pending",
            ["linuxPackageConsumption"] = "pending",
            ["windowsPackageConsumption"] = "pending",
        };
        foreach (var (key, value) in platform)
        {
            manifest[key] = value;
        }

        var path = Path.Combine(directory, name);
        CandidateManifest.Write(path, manifest);
        return path;
    }
}
