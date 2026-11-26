using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace SharedKernel.Versioning;

/// <summary>Reads declared NuGet groups and bundled tool runtime targets from a verified package.</summary>
/// <param name="Groups">The NuGet groups keyed by target framework, with an empty key for ungrouped dependencies.</param>
/// <param name="ToolTargets">The runtime libraries keyed by dependency manifest and target.</param>
internal sealed record CandidateArchiveDependencies(
    Dictionary<string, Dictionary<string, CandidateArchiveDependencies.Dependency>> Groups,
    Dictionary<CandidateArchiveDependencies.RuntimeTarget, Dictionary<string, CandidateArchiveDependencies.RuntimeLibrary>> ToolTargets)
{
    /// <summary>A dependency edge and its declared constraint and asset qualifiers.</summary>
    /// <param name="Version">The declared version or range when present.</param>
    /// <param name="Include">The declared included assets when present.</param>
    /// <param name="Exclude">The declared excluded assets when present.</param>
    internal sealed record Dependency(string? Version, string? Include, string? Exclude);

    /// <summary>Identifies one target in a bundled tool's dependency manifest.</summary>
    /// <param name="Manifest">The entry path within the package.</param>
    /// <param name="Target">The target name in the JSON manifest.</param>
    internal sealed record RuntimeTarget(string Manifest, string Target);

    /// <summary>One runtime library in a bundled tool target.</summary>
    /// <param name="Version">The library version in the target.</param>
    /// <param name="Dependencies">The library's declared dependency edges.</param>
    internal sealed record RuntimeLibrary(string Version, Dictionary<string, Dependency> Dependencies);

    /// <summary>Reads both dependency surfaces from the snapshot without restoring the package.</summary>
    /// <exception cref="ArgumentException">A dependency group or tool runtime target is invalid.</exception>
    internal static CandidateArchiveDependencies Read(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var archive = ZipFile.OpenRead(path);
        var nuspec = LocalFeed.ReadNuspec(archive, path);
        var groups = new Dictionary<string, Dictionary<string, Dependency>>(StringComparer.OrdinalIgnoreCase);
        var metadata = nuspec.Root?.Elements().SingleOrDefault(static node => node.Name.LocalName == "metadata");
        var dependencyNodes = metadata?.Elements().SingleOrDefault(static node => node.Name.LocalName == "dependencies");
        var ungrouped = ReadDependencies(dependencyNodes?.Elements().Where(static node => node.Name.LocalName != "group") ?? [], path);
        if (ungrouped.Count > 0)
        {
            groups.Add(string.Empty, ungrouped);
        }

        foreach (var group in dependencyNodes?.Elements().Where(static node => node.Name.LocalName == "group") ?? [])
        {
            var framework = (string?)group.Attribute("targetFramework") ?? string.Empty;
            if (!groups.TryAdd(framework, ReadDependencies(group.Elements(), path)))
            {
                throw new ArgumentException($"{path}: duplicate NuGet dependency group '{framework}'.");
            }
        }

        return new CandidateArchiveDependencies(groups, ReadToolTargets(archive, path, ct));
    }

    private static Dictionary<string, Dependency> ReadDependencies(IEnumerable<XElement> entries, string path)
    {
        var dependencies = new Dictionary<string, Dependency>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in entries)
        {
            var id = (string?)dependency.Attribute("id");
            if (dependency.Name.LocalName != "dependency" || string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"{path}: invalid NuGet dependency entry.");
            }

            if (!dependencies.TryAdd(id,
                new Dependency((string?)dependency.Attribute("version"), (string?)dependency.Attribute("include"), (string?)dependency.Attribute("exclude"))))
            {
                throw new ArgumentException($"{path}: duplicate NuGet dependency '{id}' in one group.");
            }
        }

        return dependencies;
    }

    private static Dictionary<RuntimeTarget, Dictionary<string, RuntimeLibrary>> ReadToolTargets(
        ZipArchive archive, string path, CancellationToken ct)
    {
        var result = new Dictionary<RuntimeTarget, Dictionary<string, RuntimeLibrary>>();
        foreach (var entry in archive.Entries.Where(static entry =>
            entry.FullName.EndsWith(".deps.json", StringComparison.Ordinal) && entry.FullName.Split('/') is ["tools", _, "any", _]))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var stream = entry.Open();
                if (JsonNode.Parse(stream) is not JsonObject document || document["targets"] is not JsonObject targets)
                {
                    throw new ArgumentException("Expected a runtime targets object.");
                }

                foreach (var (target, libraries) in targets)
                {
                    var key = new RuntimeTarget(entry.FullName, target);
                    if (!result.TryAdd(key, ReadToolLibraries(libraries, ct)))
                    {
                        throw new ArgumentException($"Duplicate runtime target '{target}'.");
                    }
                }
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
            {
                throw new ArgumentException($"{path}: invalid {entry.FullName}: {exception.Message}", exception);
            }
        }

        return result;
    }

    private static Dictionary<string, RuntimeLibrary> ReadToolLibraries(JsonNode? libraries, CancellationToken ct)
    {
        if (libraries is not JsonObject entries)
        {
            throw new ArgumentException("Expected a runtime libraries object.");
        }

        var result = new Dictionary<string, RuntimeLibrary>(StringComparer.OrdinalIgnoreCase);
        foreach (var (identity, value) in entries)
        {
            ct.ThrowIfCancellationRequested();
            var separator = identity.IndexOf('/', StringComparison.Ordinal);
            if (separator <= 0 || separator == identity.Length - 1 || value is not JsonObject library)
            {
                throw new ArgumentException($"Invalid runtime library '{identity}'.");
            }

            var id = identity[..separator];
            var dependencies = new Dictionary<string, Dependency>(StringComparer.OrdinalIgnoreCase);
            if (library["dependencies"] is { } rawDependencies)
            {
                foreach (var (dependencyId, rawVersion) in rawDependencies.AsObject())
                {
                    if (rawVersion is not JsonValue version || !version.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text) ||
                        !dependencies.TryAdd(dependencyId, new Dependency(text, null, null)))
                    {
                        throw new ArgumentException($"Invalid runtime dependency '{dependencyId}' of '{identity}'.");
                    }
                }
            }

            if (!result.TryAdd(id, new RuntimeLibrary(identity[(separator + 1)..], dependencies)))
            {
                throw new ArgumentException($"Duplicate runtime library '{id}' in one target.");
            }
        }

        return result;
    }
}
