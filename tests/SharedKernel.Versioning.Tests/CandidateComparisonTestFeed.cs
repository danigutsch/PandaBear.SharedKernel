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
        foreach (var (id, hasAssembly) in entries)
        {
            var path = NuGetPackageBuilder.WritePackage(DirectoryPath, id, version);
            if (hasAssembly)
            {
                await using var archive = await ZipFile.OpenAsync(path, ZipArchiveMode.Update, ct);
                archive.CreateEntry($"lib/{TestTargetFramework.Moniker}/Probe.dll");
            }
        }

        return await WriteManifest(version, revision, ct);
    }

    /// <summary>Writes an archive whose nuspec contains the supplied dependency groups.</summary>
    internal string WriteGroupedPackage(string id, string version, string dependencies)
    {
        var path = Path.Combine(DirectoryPath, $"{id}.{version}.nupkg");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open());
        writer.Write($"""
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{id}</id>
                <version>{version}</version>
                <dependencies>
                  {dependencies}
                </dependencies>
              </metadata>
            </package>
            """);
        return path;
    }

    /// <summary>Writes the manifest of all NuGet archives already in the fixture directory.</summary>
    internal async Task<string> WriteManifest(string version, string revision, CancellationToken ct)
    {
        var hashes = new JsonObject();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.nupkg").Order(StringComparer.Ordinal))
        {
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
