using System.Text.Json;

namespace PandaBear.Repositories;

/// <summary>The selected package graph from one project's NuGet assets document.</summary>
internal sealed class NuGetRestoreGraph(Dictionary<string, Dictionary<string, NuGetRestorePackage>> targets)
{
    private static readonly string[] AssetKinds =
    [
        "build", "buildMultiTargeting", "buildTransitive", "compile", "contentFiles", "frameworkAssemblies",
        "frameworkReferences", "native", "resource", "runtime", "runtimeTargets", "tools",
    ];

    private readonly Dictionary<string, Dictionary<string, NuGetRestorePackage>> targets = targets;

    /// <summary>The graph for a project absent from one side of a comparison.</summary>
    internal static NuGetRestoreGraph Empty { get; } = new(new(StringComparer.Ordinal));

    /// <summary>Reads the selected packages and their evaluated classification per target.</summary>
    /// <exception cref="InvalidDataException">The assets version or a package identity is invalid, or no targets were found.</exception>
    internal static NuGetRestoreGraph Read(JsonElement assets)
    {
        if (assets.GetProperty("version").GetInt32() is not (3 or 4))
        {
            throw new InvalidDataException("Unsupported NuGet assets document version.");
        }

        var libraries = assets.GetProperty("libraries");
        var frameworks = assets.GetProperty("project").GetProperty("frameworks");
        var graph = new Dictionary<string, Dictionary<string, NuGetRestorePackage>>(StringComparer.Ordinal);
        foreach (var target in assets.GetProperty("targets").EnumerateObject())
        {
            var separator = target.Name.IndexOf('/', StringComparison.Ordinal);
            var framework = separator < 0 ? target.Name : target.Name[..separator];
            var direct = Names(frameworks.GetProperty(framework), "dependencies");
            var pinned = assets.TryGetProperty("centralTransitiveDependencyGroups", out var pins)
                ? Names(pins, framework)
                : [];
            var packages = new Dictionary<string, NuGetRestorePackage>(StringComparer.OrdinalIgnoreCase);

            foreach (var library in target.Value.EnumerateObject())
            {
                if (library.Value.GetProperty("type").GetString() != "package")
                {
                    continue;
                }

                var split = library.Name.IndexOf('/', StringComparison.Ordinal);
                if (split <= 0 || split == library.Name.Length - 1)
                {
                    throw new InvalidDataException("NuGet package identity must contain a name and version.");
                }

                var name = library.Name[..split];
                var version = library.Name[(split + 1)..];
                var contentHash = ReadString(libraries.GetProperty(library.Name).GetProperty("sha512"));
                var classification = Classify(name, direct, pinned);
                packages.Add(name, new(version, classification, contentHash, ReadDependencies(library.Value), ReadAssets(library.Value)));
            }

            graph.Add(target.Name, packages);
        }

        if (graph.Count == 0)
        {
            throw new InvalidDataException("The NuGet assets document contains no targets.");
        }

        return new(graph);
    }

    /// <summary>Reports additions, removals, and changed packages in deterministic order.</summary>
    internal IReadOnlyList<NuGetRestoreChange> Compare(NuGetRestoreGraph candidate)
    {
        List<NuGetRestoreChange> changes = [];
        foreach (var target in targets.Keys.Union(candidate.targets.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var before = targets.GetValueOrDefault(target);
            var after = candidate.targets.GetValueOrDefault(target);
            var names = (before?.Keys.AsEnumerable() ?? [])
                .Union(after?.Keys.AsEnumerable() ?? [], StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                var left = before?.GetValueOrDefault(name);
                var right = after?.GetValueOrDefault(name);
                if (left is null || right is null || !left.SameAs(right))
                {
                    changes.Add(new(target, name, left, right));
                }
            }
        }

        return changes;
    }

    /// <summary>Finds the package's resolved identity and classification for every target that selects it.</summary>
    internal IEnumerable<(string Target, NuGetRestorePackage Package)> FindPackage(string packageId)
    {
        foreach (var (target, packages) in targets.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (packages.TryGetValue(packageId, out var package))
            {
                yield return (target, package);
            }
        }
    }

    /// <summary>Finds direct references that request a resolved transitive version in any target.</summary>
    internal IEnumerable<string> FindDirectOwners(string packageId, string? version)
    {
        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var packages in targets.Values)
        {
            if (!packages.TryGetValue(packageId, out var affected)
                || affected.Classification == NuGetRestoreClassification.Direct
                || (version is not null && !string.Equals(affected.Version, version, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { packageId };
            var pending = new Queue<string>();
            pending.Enqueue(packageId);
            while (pending.TryDequeue(out var dependency))
            {
                foreach (var (name, package) in packages)
                {
                    if (!package.Dependencies.Any(edge => string.Equals(edge.Key, dependency, StringComparison.OrdinalIgnoreCase))
                        || !seen.Add(name))
                    {
                        continue;
                    }

                    if (package.Classification == NuGetRestoreClassification.Direct)
                    {
                        owners.Add(name);
                    }
                    else
                    {
                        pending.Enqueue(name);
                    }
                }
            }
        }

        return owners;
    }

    private static HashSet<string> Names(JsonElement element, string key) =>
        element.TryGetProperty(key, out var values)
            ? values.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

    private static NuGetRestoreClassification Classify(string name, HashSet<string> direct, HashSet<string> pinned)
    {
        if (direct.Contains(name))
        {
            return NuGetRestoreClassification.Direct;
        }

        return pinned.Contains(name) ? NuGetRestoreClassification.CentralTransitive : NuGetRestoreClassification.Transitive;
    }

    private static KeyValuePair<string, string>[] ReadDependencies(JsonElement library) =>
        library.TryGetProperty("dependencies", out var dependencies)
            ? dependencies.EnumerateObject().OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
                .Select(property => new KeyValuePair<string, string>(property.Name, ReadString(property.Value))).ToArray()
            : [];

    private static List<KeyValuePair<string, string>> ReadAssets(JsonElement library)
    {
        List<KeyValuePair<string, string>> assets = [];
        foreach (var kind in AssetKinds)
        {
            if (!library.TryGetProperty(kind, out var selected))
            {
                continue;
            }

            var paths = selected.ValueKind == JsonValueKind.Array
                ? selected.EnumerateArray().Select(ReadString)
                : selected.EnumerateObject().Select(property => property.Name);
            assets.AddRange(paths.Order(StringComparer.Ordinal).Select(path => new KeyValuePair<string, string>(kind, path)));
        }

        return assets;
    }

    private static string ReadString(JsonElement value) =>
        value.GetString() ?? throw new InvalidDataException("A required NuGet assets value is null.");
}
