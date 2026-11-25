using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedKernel.IO;
using static SharedKernel.Versioning.CandidateArchiveDependencies;

namespace SharedKernel.Versioning;

/// <summary>Compares the dependency metadata in two hash-verified candidate feeds.</summary>
internal static class CandidateDependencyComparison
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    /// <summary>Writes separate dependency evidence and returns whether the declared graphs match.</summary>
    /// <exception cref="ArgumentException">A path or candidate is invalid.</exception>
    /// <exception cref="IOException">The report path exists or the report cannot be written.</exception>
    internal static async Task<bool> Run(string baselineManifest, string candidateManifest, string outputPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateManifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ct.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(outputPath);
        if (File.Exists(destination))
        {
            throw new IOException($"The dependency comparison report path already exists: {destination}.");
        }

        var scratch = Directory.CreateTempSubdirectory("sharedkernel-dependency-comparison-").FullName;
        JsonObject report;
        bool passed;
        try
        {
            var baseline = await CandidateArchiveSnapshot.Create(baselineManifest, Path.Combine(scratch, "baseline"),
                requireCoordinatedDependencies: false, ct).ConfigureAwait(false);
            var candidate = await CandidateArchiveSnapshot.Create(candidateManifest, Path.Combine(scratch, "candidate"),
                requireCoordinatedDependencies: false, ct).ConfigureAwait(false);
            var packages = ComparePackages(baseline, candidate, ct);
            passed = packages.Count == 0;
            report = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["status"] = passed ? "passed" : "differences",
                ["baseline"] = baseline.Identity,
                ["candidate"] = candidate.Identity,
                ["packages"] = packages,
            };
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }

        var bytes = Encoding.UTF8.GetBytes(report.ToJsonString(ReportOptions) + "\n");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (!await AtomicFile.CreateIfAbsent(destination, (stream, token) => stream.WriteAsync(bytes.AsMemory(), token), ct).ConfigureAwait(false))
        {
            throw new IOException($"The dependency comparison report path already exists: {destination}.");
        }

        return passed;
    }

    private static JsonArray ComparePackages(CandidateArchiveSnapshot baseline, CandidateArchiveSnapshot candidate, CancellationToken ct)
    {
        var report = new JsonArray();
        var versions = (Baseline: baseline.Version, Candidate: candidate.Version);
        foreach (var id in Keys(baseline.Packages, candidate.Packages))
        {
            var baselinePath = baseline.Packages.GetValueOrDefault(id);
            var candidatePath = candidate.Packages.GetValueOrDefault(id);
            var before = baselinePath is null ? null : Read(baselinePath, ct);
            var after = candidatePath is null ? null : Read(candidatePath, ct);
            var groups = CompareGroups(before?.Groups, after?.Groups, versions);
            var targets = CompareToolTargets(before?.ToolTargets, after?.ToolTargets, versions);
            if (before is null || after is null || groups.Count > 0 || targets.Count > 0)
            {
                report.Add((JsonNode)new JsonObject
                {
                    ["packageId"] = id,
                    ["status"] = ChangeStatus(before, after),
                    ["dependencyGroups"] = groups,
                    ["toolRuntimeTargets"] = targets,
                });
            }
        }

        return report;
    }

    private static JsonArray CompareGroups(
        Dictionary<string, Dictionary<string, Dependency>>? before,
        Dictionary<string, Dictionary<string, Dependency>>? after,
        (string Baseline, string Candidate) versions)
    {
        var groups = new JsonArray();
        foreach (var framework in Keys(before, after))
        {
            var previous = before?.GetValueOrDefault(framework);
            var current = after?.GetValueOrDefault(framework);
            var dependencies = CompareEdges(previous, current, versions);
            if (previous is null || current is null || dependencies.Count > 0)
            {
                groups.Add((JsonNode)new JsonObject
                {
                    ["targetFramework"] = framework.Length == 0 ? null : framework,
                    ["status"] = ChangeStatus(previous, current),
                    ["dependencies"] = dependencies,
                });
            }
        }

        return groups;
    }

    private static JsonArray CompareToolTargets(
        Dictionary<RuntimeTarget, Dictionary<string, RuntimeLibrary>>? before,
        Dictionary<RuntimeTarget, Dictionary<string, RuntimeLibrary>>? after,
        (string Baseline, string Candidate) versions)
    {
        var targets = new JsonArray();
        var keys = (before?.Keys.AsEnumerable() ?? []).Union(after?.Keys.AsEnumerable() ?? [])
            .OrderBy(static key => key.Manifest, StringComparer.Ordinal).ThenBy(static key => key.Target, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var previous = before?.GetValueOrDefault(key);
            var current = after?.GetValueOrDefault(key);
            var libraries = CompareToolLibraries(previous, current, versions);
            if (previous is null || current is null || libraries.Count > 0)
            {
                targets.Add((JsonNode)new JsonObject
                {
                    ["runtimeManifest"] = key.Manifest,
                    ["target"] = key.Target,
                    ["status"] = ChangeStatus(previous, current),
                    ["libraries"] = libraries,
                });
            }
        }

        return targets;
    }

    private static JsonArray CompareToolLibraries(
        Dictionary<string, RuntimeLibrary>? before, Dictionary<string, RuntimeLibrary>? after,
        (string Baseline, string Candidate) versions)
    {
        var libraries = new JsonArray();
        foreach (var id in Keys(before, after))
        {
            var previous = before?.GetValueOrDefault(id);
            var current = after?.GetValueOrDefault(id);
            var dependencies = CompareEdges(previous?.Dependencies, current?.Dependencies, versions, bundledTool: true);
            if (previous is null || current is null || dependencies.Count > 0 ||
                ComparableVersion(id, previous.Version, versions.Baseline, bundledTool: true) !=
                ComparableVersion(id, current.Version, versions.Candidate, bundledTool: true))
            {
                libraries.Add((JsonNode)new JsonObject
                {
                    ["libraryId"] = id,
                    ["status"] = ChangeStatus(previous, current),
                    ["baselineVersion"] = previous?.Version,
                    ["candidateVersion"] = current?.Version,
                    ["dependencies"] = dependencies,
                });
            }
        }

        return libraries;
    }

    private static JsonArray CompareEdges(
        Dictionary<string, Dependency>? before, Dictionary<string, Dependency>? after,
        (string Baseline, string Candidate) versions, bool bundledTool = false)
    {
        var edges = new JsonArray();
        foreach (var id in Keys(before, after))
        {
            var previous = before?.GetValueOrDefault(id);
            var current = after?.GetValueOrDefault(id);
            if (previous is null || current is null || previous.Include != current.Include || previous.Exclude != current.Exclude ||
                ComparableVersion(id, previous.Version, versions.Baseline, bundledTool) !=
                ComparableVersion(id, current.Version, versions.Candidate, bundledTool))
            {
                edges.Add((JsonNode)new JsonObject
                {
                    ["dependencyId"] = id,
                    ["status"] = ChangeStatus(previous, current),
                    ["baseline"] = Evidence(previous),
                    ["candidate"] = Evidence(current),
                });
            }
        }

        return edges;
    }

    private static IEnumerable<string> Keys<T>(IReadOnlyDictionary<string, T>? before, IReadOnlyDictionary<string, T>? after) =>
        (before?.Keys ?? []).Union(after?.Keys ?? [], StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);

    private static (string? Version, CohortMatch Match) ComparableVersion(string id, string? version, string cohort, bool bundledTool)
    {
        if (id.StartsWith("PandaBear.SharedKernel.", StringComparison.OrdinalIgnoreCase) ||
            (bundledTool && id.StartsWith("SharedKernel.", StringComparison.OrdinalIgnoreCase)))
        {
            if (version == cohort)
            {
                return (null, CohortMatch.Version);
            }

            if (version == $"[{cohort}]")
            {
                return (null, CohortMatch.ExactRange);
            }
        }

        return (version, CohortMatch.None);
    }

    private static string ChangeStatus(object? before, object? after) =>
        before is null ? "added" : after is null ? "removed" : "changed";

    private static JsonObject? Evidence(Dependency? dependency) => dependency is null
        ? null
        : new JsonObject
        {
            ["version"] = dependency.Version,
            ["include"] = dependency.Include,
            ["exclude"] = dependency.Exclude,
        };

    private enum CohortMatch
    {
        None,
        Version,
        ExactRange,
    }
}
