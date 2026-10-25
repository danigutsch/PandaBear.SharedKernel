using System.Text.Json.Nodes;
using PandaBear.Tooling;

namespace PandaBear.Versioning.Tool;

/// <summary>
/// Merges the per-platform results that <c>consume-candidate</c> recorded in separate evidence files into a candidate
/// manifest, so a consumer pin does not carry hand-edited results.
/// </summary>
internal static class CandidateEvidenceCommand
{
    private static readonly string[] PlatformResults =
    [
        "linuxPackageConsumption",
        "windowsPackageConsumption",
        "linuxNativeAot",
        "windowsNativeAot",
    ];

    private static readonly string[] Identity = ["libraryRevision", "libraryDirty", "packageVersion"];

    /// <exception cref="ArgumentException">
    /// An evidence file describes another candidate, or records a failed platform result. Nothing is written.
    /// </exception>
    public static async Task Run(string candidate, IReadOnlyList<string> evidenceFiles, string destination, TextWriter output, CancellationToken ct)
    {
        var merged = await Read(candidate, ct).ConfigureAwait(false);
        foreach (var file in evidenceFiles)
        {
            var evidence = await Read(file, ct).ConfigureAwait(false);
            foreach (var key in Identity.Append("packageSha256"))
            {
                if (merged[key]?.ToJsonString() != evidence[key]?.ToJsonString())
                {
                    throw new ArgumentException($"Evidence {file} does not describe the candidate: {key} differs.");
                }
            }

            foreach (var key in PlatformResults)
            {
                var result = evidence[key]?.GetValue<string>();
                if (result == "failed")
                {
                    throw new ArgumentException($"Evidence {file} records {key} as failed.");
                }

                if (result == "passed")
                {
                    merged[key] = result;
                    await output.WriteLineAsync($"{key}: passed").ConfigureAwait(false);
                }
            }
        }

        CandidateManifest.Write(destination, merged);
    }

    // Evidence travels without the packages, so the package files cannot be hashed here; the recorded hashes must match.
    private static async Task<JsonObject> Read(string path, CancellationToken ct) =>
        JsonNode.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false))?.AsObject()
            ?? throw new ArgumentException($"Candidate manifest is empty: {path}.");
}
