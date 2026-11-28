using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace SharedKernel.Versioning.Tests;

/// <summary>Creates one real assembly and a paired portable PDB in manifest-hashed NuGet archives.</summary>
internal sealed class CandidateSourceLinkTestFeed : IDisposable
{
    /// <summary>The full revision in the manifest and archive metadata.</summary>
    internal const string CandidateRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    /// <summary>A distinct full revision for a mismatched mapping.</summary>
    internal const string OtherRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    /// <summary>The package and symbol archive identity.</summary>
    internal const string PackageId = "PandaBear.SharedKernel.Probe";

    /// <summary>The tool archive identity used by embedded symbol cases.</summary>
    internal const string ToolPackageId = PackageId + ".Tool";
    /// <summary>The coordinated package and symbol version.</summary>
    internal const string PackageVersion = "1.0.0-alpha.1";
    /// <summary>The caller-supplied content URL pattern.</summary>
    internal const string SourcePattern = "https://content.example.test/owner/repo/{revision}/*";

    private static readonly Guid SourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");
    private static readonly BlobContentId PairedPdbId = new(new Guid("6f89d60a-b824-422e-a4c1-66da50110d1d"), 1);
    private static readonly BlobContentId UnpairedPdbId = new(new Guid("f731880d-982c-4f58-9a46-71aa31f3eb8a"), 1);
    private static readonly DateTimeOffset ArchiveTimestamp = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly CandidateComparisonTestFeed _feed = new();

    /// <summary>Gets the disposable feed path.</summary>
    internal string DirectoryPath => _feed.DirectoryPath;

    /// <summary>Gets the exact test symbol filename included in the explicit inventory.</summary>
    internal static string SymbolFileName => $"{PackageId}.{PackageVersion}.snupkg";

    /// <summary>Writes the candidate, symbol inventory and paired binaries. A null source revision omits the mapping.</summary>
    internal async Task<(string Manifest, string Symbols)> Write(
        string? sourceRevision, bool mismatchedAssembly, CancellationToken ct, bool includeTool = false,
        string? duplicateToolMember = null, string? sourceLinkJson = null, string symbolMember = "Probe.pdb")
    {
        var module = new MetadataBuilder();
        module.AddModule(0, module.GetOrAddString("Probe.dll"),
            module.GetOrAddGuid(new Guid("2a29f3f8-bbe6-4902-9b04-e65e0bd99e37")), default, default);
        module.AddAssembly(module.GetOrAddString("Probe"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        module.AddTypeDefinition(0, default, module.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

        var symbols = new MetadataBuilder();
        symbols.AddDocument(symbols.GetOrAddDocumentName("/_/src/Probe.cs"), default, default, default);
        if (sourceLinkJson is null && sourceRevision is not null)
        {
            var url = SourcePattern.Replace("{revision}", sourceRevision, StringComparison.Ordinal);
            sourceLinkJson = $$$"""{"documents":{"/_/*":"{{{url}}}"}}""";
        }

        if (sourceLinkJson is not null)
        {
            var mapping = Encoding.UTF8.GetBytes(sourceLinkJson);
            symbols.AddCustomDebugInformation(MetadataTokens.EntityHandle(TableIndex.Module, 1),
                symbols.GetOrAddGuid(SourceLinkKind), symbols.GetOrAddBlob(mapping));
        }

        var pdb = new BlobBuilder();
        var pdbId = new PortablePdbBuilder(symbols, module.GetRowCounts(), default, static _ => PairedPdbId).Serialize(pdb);
        var debug = new DebugDirectoryBuilder();
        debug.AddCodeViewEntry("Probe.pdb", mismatchedAssembly ? UnpairedPdbId : pdbId, portablePdbVersion: 0x0100);
        var image = new BlobBuilder();
        new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(module), new BlobBuilder(),
            debugDirectoryBuilder: debug, deterministicIdProvider: static _ => PairedPdbId).Serialize(image);

        WriteArchive($"{PackageId}.{PackageVersion}.nupkg", "Probe.dll", image);
        WriteArchive(SymbolFileName, symbolMember, pdb);
        if (includeTool)
        {
            WriteToolArchive(image, pdb, duplicateToolMember);
        }

        var manifest = await _feed.WriteManifest(PackageVersion, CandidateRevision, ct);
        var symbolHash = await CandidateManifest.HashPackage(Path.Combine(DirectoryPath, SymbolFileName), ct);
        var inventory = Path.Combine(DirectoryPath, "symbols.sha256");
        await File.WriteAllTextAsync(inventory, $"{symbolHash}  ./{SymbolFileName}\n", ct);
        return (manifest, inventory);
    }

    private void WriteArchive(string name, string member, BlobBuilder contents)
    {
        using var archive = ZipFile.Open(Path.Combine(DirectoryPath, name), ZipArchiveMode.Create);
        WriteMetadata(archive, PackageId);
        WriteContents(archive, $"lib/{TestTargetFramework.Moniker}/{member}", contents);
    }

    private void WriteToolArchive(BlobBuilder image, BlobBuilder symbols, string? duplicate)
    {
        using var archive = ZipFile.Open(Path.Combine(DirectoryPath, $"{ToolPackageId}.{PackageVersion}.nupkg"), ZipArchiveMode.Create);
        WriteMetadata(archive, ToolPackageId);
        var prefix = $"tools/{TestTargetFramework.Moniker}/any/";
        using (var writer = new StreamWriter(CreateEntry(archive, prefix + "DotnetToolSettings.xml").Open()))
        {
            writer.Write("""
                <DotNetCliTool Version="1">
                  <Commands>
                    <Command Name="probe" EntryPoint="Probe.dll" Runner="dotnet" />
                  </Commands>
                </DotNetCliTool>
                """);
        }

        WriteContents(archive, prefix + "Probe.dll", image);
        WriteContents(archive, prefix + "Probe.pdb", symbols);
        if (duplicate is not null)
        {
            WriteContents(archive, prefix + duplicate, duplicate == "Probe.dll" ? image : symbols);
        }
    }

    private static void WriteMetadata(ZipArchive archive, string packageId)
    {
        using var writer = new StreamWriter(CreateEntry(archive, packageId + ".nuspec").Open());
        writer.Write($"""
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{packageId}</id>
                <version>{PackageVersion}</version>
                <authors>Fixture</authors>
                <description>Source Link archive fixture.</description>
                <repository type="git" url="https://git.example.test/owner/repo" commit="{CandidateRevision}" />
              </metadata>
            </package>
            """);
    }

    private static void WriteContents(ZipArchive archive, string path, BlobBuilder contents)
    {
        using var stream = CreateEntry(archive, path).Open();
        contents.WriteContentTo(stream);
    }

    private static ZipArchiveEntry CreateEntry(ZipArchive archive, string path)
    {
        var entry = archive.CreateEntry(path);
        entry.LastWriteTime = ArchiveTimestamp;
        return entry;
    }

    /// <inheritdoc />
    public void Dispose() => _feed.Dispose();
}
