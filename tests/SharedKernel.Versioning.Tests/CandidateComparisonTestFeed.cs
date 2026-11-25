using System.IO.Compression;
using System.Text.Json.Nodes;

namespace SharedKernel.Versioning.Tests;

/// <summary>Creates manifest-verified NuGet archives for comparison orchestration tests.</summary>
internal sealed class CandidateComparisonTestFeed : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>Gets the disposable directory that contains the manifest and packages.</summary>
    internal string DirectoryPath => _directory.Path;

    /// <summary>Writes package metadata and archive hashes for one candidate.</summary>
    internal async Task<string> Write(string version, string revision, CancellationToken ct, params (string Id, bool HasAssembly)[] entries)
    {
        var hashes = new JsonObject();
        foreach (var (id, hasAssembly) in entries)
        {
            var path = NuGetPackageBuilder.WritePackage(DirectoryPath, id, version);
            if (hasAssembly)
            {
                await using var archive = await ZipFile.OpenAsync(path, ZipArchiveMode.Update, ct);
                archive.CreateEntry($"lib/{TestTargetFramework.Moniker}/Probe.dll");
            }

            hashes[Path.GetFileName(path)] = await CandidateManifest.HashPackage(path, ct);
        }

        var manifest = Path.Combine(DirectoryPath, CandidateManifest.FileName);
        new CandidateManifest(new JsonObject
        {
            ["packageVersion"] = version,
            ["libraryRevision"] = revision,
            ["packageSha256"] = hashes,
        }).Save(manifest);
        return manifest;
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
