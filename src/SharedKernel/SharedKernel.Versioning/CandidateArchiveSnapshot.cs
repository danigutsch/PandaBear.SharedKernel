using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

namespace SharedKernel.Versioning;

/// <summary>Holds the verified local archive paths and identity of one candidate comparison input.</summary>
/// <param name="Identity">The evidence binding the input manifest to its archives.</param>
/// <param name="Version">The coordinated package version.</param>
/// <param name="Packages">The verified snapshot paths keyed by package ID.</param>
internal sealed record CandidateArchiveSnapshot(JsonObject Identity, string Version, Dictionary<string, string> Packages)
{
    /// <summary>Copies exact candidate bytes into a private directory and verifies its package inventory and identities.</summary>
    /// <exception cref="ArgumentException">The candidate evidence or archive identity is invalid.</exception>
    internal static async Task<CandidateArchiveSnapshot> Create(
        string manifestPath, string snapshotDirectory, bool requireCoordinatedDependencies, CancellationToken ct)
    {
        var source = Path.GetFullPath(manifestPath);
        var sourceDirectory = Path.GetDirectoryName(source)!;
        Directory.CreateDirectory(snapshotDirectory);
        var snapshot = Path.Combine(snapshotDirectory, CandidateManifest.FileName);
        try
        {
            File.Copy(source, snapshot);
            var evidence = CandidateManifest.Load(snapshot, sourceDirectory).Evidence;
            var version = RequiredProperty(evidence, "packageVersion");
            _ = SemanticVersion.Parse(version);
            var revision = RequiredProperty(evidence, "libraryRevision");
            if (revision.Length is not (40 or 64) || !revision.All(char.IsAsciiHexDigit))
            {
                throw new ArgumentException("Candidate libraryRevision must be a full Git object ID.");
            }

            var hashes = evidence["packageSha256"]!.AsObject();
            CandidateManifest.RequireListedPackagesOnly(sourceDirectory, hashes);
            var paths = hashes.Select(entry => Path.Combine(snapshotDirectory, entry.Key)).ToArray();
            var packages = new Dictionary<string, string>(
                LocalFeed.ReadPackagePaths(paths, version, requireCoordinatedDependencies), StringComparer.OrdinalIgnoreCase);
            var identity = new JsonObject
            {
                ["packageVersion"] = version,
                ["libraryRevision"] = revision,
                ["manifestSha256"] = await CandidateManifest.HashPackage(snapshot, ct).ConfigureAwait(false),
                ["packageSha256"] = hashes.DeepClone(),
            };
            return new CandidateArchiveSnapshot(identity, version, packages);
        }
        catch (Exception exception) when (exception is JsonException or XmlException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new ArgumentException($"Candidate evidence cannot be read from {source}: {exception.Message}", exception);
        }
    }

    private static string RequiredProperty(JsonObject evidence, string property) =>
        evidence[property] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new ArgumentException($"Candidate manifest is missing a valid {property}.");
}
