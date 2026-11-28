using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using SharedKernel.IO;

namespace SharedKernel.Versioning;

/// <summary>Checks Source Link in explicitly selected package symbols without starting tools or reaching a source server.</summary>
internal static class CandidateSourceLinkVerification
{
    private const int InventoryLimit = 1024 * 1024;
    private const string SettingsFileName = "DotnetToolSettings.xml";
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    /// <summary>Writes new source provenance evidence for the exact input archives.</summary>
    /// <exception cref="ArgumentException">The selected evidence or source pattern is invalid.</exception>
    /// <exception cref="ArgumentNullException">The tool package list is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The report cannot be created.</exception>
    internal static async Task<bool> Run(
        string candidateManifest, string symbolInventory, string sourcePattern, IReadOnlyList<string> toolPackages,
        string outputPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbolInventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(toolPackages);
        ct.ThrowIfCancellationRequested();

        var destination = Path.GetFullPath(outputPath);
        if (File.Exists(destination))
        {
            throw new IOException("The Source Link report path already exists.");
        }

        var scratch = Directory.CreateTempSubdirectory("sharedkernel-source-link-").FullName;
        JsonObject report;
        bool passed;
        try
        {
            var snapshot = await CandidateArchiveSnapshot.Create(candidateManifest, Path.Combine(scratch, "candidate"),
                requireCoordinatedDependencies: false, ct).ConfigureAwait(false);
            var revision = snapshot.Identity["libraryRevision"]!.GetValue<string>();
            var prefix = PortableSourceLink.Bind(sourcePattern, revision);
            var selectedTools = toolPackages.Order(StringComparer.Ordinal).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var hashes = await ReadHashes(symbolInventory, ct).ConfigureAwait(false);
            var results = new JsonArray();
            HashSet<string> selectedAssemblies = new(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, hash) in hashes.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                var symbolPath = Path.Combine(scratch, name);
                var input = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(candidateManifest))!, name);
                File.Copy(input, symbolPath);
                if (!string.Equals(await CandidateManifest.HashPackage(symbolPath, ct).ConfigureAwait(false), hash, StringComparison.Ordinal))
                {
                    throw new ArgumentException("A selected symbol archive does not match its recorded hash.");
                }

                var symbols = await ZipFile.OpenReadAsync(symbolPath, ct).ConfigureAwait(false);
                await using var symbolsOwner = symbols.ConfigureAwait(false);
                var metadata = LocalFeed.ReadNuspec(symbols, name);
                var id = metadata.Descendants().FirstOrDefault(static item => item.Name.LocalName == "id")?.Value;
                var version = metadata.Descendants().FirstOrDefault(static item => item.Name.LocalName == "version")?.Value;
                if (id is null || !snapshot.Packages.TryGetValue(id, out var package)
                    || version != snapshot.Version
                    || !string.Equals(name, $"{id}.{version}.snupkg", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("A selected symbol archive does not identify a package in the candidate.");
                }

                var assemblies = await ZipFile.OpenReadAsync(package, ct).ConfigureAwait(false);
                await using var assembliesOwner = assemblies.ConfigureAwait(false);
                foreach (var entry in SymbolEntries(symbols))
                {
                    selectedAssemblies.Add(Path.GetFileNameWithoutExtension(entry.Name));
                    await Record(results, name, assemblies, entry, prefix, ct).ConfigureAwait(false);
                }
            }

            foreach (var id in selectedTools)
            {
                ct.ThrowIfCancellationRequested();
                if (!snapshot.Packages.TryGetValue(id, out var path))
                {
                    throw new ArgumentException("A selected tool package is absent from the candidate.");
                }

                var package = await ZipFile.OpenReadAsync(path, ct).ConfigureAwait(false);
                await using var packageOwner = package.ConfigureAwait(false);
                var entries = ToolSymbols(package, selectedAssemblies);
                foreach (var entry in entries)
                {
                    if (UniqueEntry(package, entry) is { } pdb)
                    {
                        await Record(results, Path.GetFileName(path), package, pdb, prefix, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        results.Add(item: Result(Path.GetFileName(path), entry, "missing-symbol"));
                    }
                }
            }

            passed = results.All(static row => row!["status"]!.GetValue<string>() == "passed");
            report = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["status"] = passed ? "passed" : "differences",
                ["candidate"] = snapshot.Identity,
                ["sourceUrlPatternSha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourcePattern))),
                ["toolPackages"] = new JsonArray([.. selectedTools.Select(static id => JsonValue.Create(id))]),
                ["symbolSha256"] = new JsonObject(hashes.OrderBy(static entry => entry.Key, StringComparer.Ordinal)
                    .Select(static entry => KeyValuePair.Create<string, JsonNode?>(entry.Key, JsonValue.Create(entry.Value)))),
                ["symbols"] = results,
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or JsonException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new ArgumentException("The selected source provenance evidence is invalid or cannot be read.", exception);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }

        var bytes = Encoding.UTF8.GetBytes(report.ToJsonString(ReportOptions) + "\n");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!await AtomicFile.CreateIfAbsent(destination, (stream, token) => stream.WriteAsync(bytes.AsMemory(), token), ct).ConfigureAwait(false))
        {
            throw new IOException("The Source Link report path already exists.");
        }

        return passed;
    }

    private static async Task<Dictionary<string, string>> ReadHashes(string path, CancellationToken ct)
    {
        var bytes = await BoundedReader.ReadFile(path, InventoryLimit, ct).ConfigureAwait(false)
            ?? throw new ArgumentException("The symbol hash inventory exceeds its byte limit.");
        var text = new UTF8Encoding(false, true).GetString(bytes);
        Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
        using var reader = new StringReader(text);
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            ct.ThrowIfCancellationRequested();
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            if (separator != 64 || !line[..separator].All(char.IsAsciiHexDigit))
            {
                throw new ArgumentException("The symbol inventory requires a SHA-256 digest and filename on each line.");
            }

            var name = line[(separator + 2)..];
            if (name.StartsWith("./", StringComparison.Ordinal))
            {
                name = name[2..];
            }

            if (!name.EndsWith(".snupkg", StringComparison.Ordinal) || !name.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '+')
                || !hashes.TryAdd(name, Convert.ToHexStringLower(Convert.FromHexString(line.AsSpan(0, separator)))))
            {
                throw new ArgumentException("The symbol inventory has an invalid or duplicate archive filename.");
            }
        }

        return hashes.Count == 0 ? throw new ArgumentException("The symbol inventory has no archives.") : hashes;
    }

    private static ZipArchiveEntry[] SymbolEntries(ZipArchive archive)
    {
        var entries = archive.Entries.Where(static entry => entry.Name.EndsWith(".pdb", StringComparison.Ordinal)).OrderBy(static entry => entry.FullName, StringComparer.Ordinal).ToArray();
        if (entries.Length == 0 || entries.Select(static entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw new ArgumentException("The selected archive has no portable PDB entries or has duplicate paths.");
        }

        return entries;
    }

    private static string[] ToolSymbols(ZipArchive package, HashSet<string> selectedAssemblies)
    {
        HashSet<string> entries = new(StringComparer.Ordinal);
        var settings = package.Entries.Where(static entry => entry.FullName.Split('/') is ["tools", _, "any", SettingsFileName]);
        foreach (var file in settings)
        {
            using var stream = file.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 64 * 1024,
            });
            var document = XDocument.Load(reader);
            var directory = file.FullName[..^SettingsFileName.Length];
            foreach (var command in document.Root?.Element("Commands")?.Elements("Command") ?? [])
            {
                var entryPoint = (string?)command.Attribute("EntryPoint");
                if (entryPoint is null || Path.GetFileName(entryPoint) != entryPoint || entryPoint.Contains('\\', StringComparison.Ordinal)
                    || !entryPoint.EndsWith(".dll", StringComparison.Ordinal))
                {
                    throw new ArgumentException("The selected tool has an invalid assembly entry point.");
                }

                entries.Add(directory + Path.ChangeExtension(entryPoint, ".pdb"));
            }

            foreach (var assembly in package.Entries.Where(entry => entry.FullName == directory + entry.Name
                         && entry.Name.EndsWith(".dll", StringComparison.Ordinal)
                         && selectedAssemblies.Contains(Path.GetFileNameWithoutExtension(entry.Name))))
            {
                entries.Add(Path.ChangeExtension(assembly.FullName, ".pdb"));
            }
        }

        return entries.Count == 0 ? throw new ArgumentException("The selected tool declares no assembly symbols.") : [.. entries.Order(StringComparer.Ordinal)];
    }

    private static async Task Record(
        JsonArray results, string archive, ZipArchive assemblies, ZipArchiveEntry symbols, Uri prefix, CancellationToken ct)
    {
        var path = Path.ChangeExtension(symbols.FullName, ".dll");
        var status = UniqueEntry(assemblies, path) is { } assembly
            ? await PortableSourceLink.Check(assembly, symbols, prefix, ct).ConfigureAwait(false)
            : "missing-assembly";
        results.Add(item: Result(archive, symbols.FullName, status));
    }

    private static ZipArchiveEntry? UniqueEntry(ZipArchive archive, string path)
    {
        var entries = archive.Entries.Where(entry => string.Equals(entry.FullName, path, StringComparison.Ordinal)).Take(2).ToArray();
        if (entries.Length > 1)
        {
            throw new ArgumentException("A selected archive has duplicate assembly or symbol member paths.");
        }

        return entries.Length == 0 ? null : entries[0];
    }

    private static JsonObject Result(string archive, string member, string status) => new JsonObject
    {
        ["archive"] = archive,
        ["member"] = member,
        ["status"] = status,
    };
}
