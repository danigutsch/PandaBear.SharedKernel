using System.Text.Json;
using System.Text.Json.Nodes;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Planned updates to the GitHub references stored in roadmap item files.</summary>
internal sealed class GitHubRoadmapReferencePlan
{
    private GitHubRoadmapReferencePlan(RoadmapProject project, IReadOnlyList<AtomicFileWrite> writes, IReadOnlyList<string> diagnostics)
    {
        Project = project;
        Writes = writes;
        Diagnostics = diagnostics;
    }

    /// <summary>Roadmap project after the planned updates.</summary>
    public RoadmapProject Project
    {
        get;
    }

    /// <summary>File writes that apply the planned updates.</summary>
    public IReadOnlyList<AtomicFileWrite> Writes
    {
        get;
    }

    /// <summary>Diagnostics found while planning.</summary>
    public IReadOnlyList<string> Diagnostics
    {
        get;
    }

    /// <summary>Plans reference updates for every item of the project from the snapshot.</summary>
    /// <param name="project">Roadmap project.</param>
    /// <param name="snapshot">GitHub issue snapshot.</param>
    public static GitHubRoadmapReferencePlan Create(RoadmapProject project, GitHubRoadmapReconcileSnapshot snapshot)
    {
        List<AtomicFileWrite> writes = [];
        List<string> diagnostics = [];
        List<RoadmapItemSnapshot> items = [];
        foreach (var item in project.Items)
        {
            if (!snapshot.ReferenceResolutions.TryGetValue(item.Id, out var resolution))
            {
                items.Add(item);
                continue;
            }

            if (!resolution.IsResolved)
            {
                diagnostics.Add($"unresolved: {item.Id} ({item.GitHubRepository ?? project.GitHubRepository}#{item.GitHubIssue}): {resolution.Diagnostic} Metadata, priority and relationships retained.");
                items.Add(item with
                {
                    GitHubReferenceUnresolved = true
                });
                continue;
            }

            var previousRepository = item.GitHubRepository ?? project.GitHubRepository!;
            var changedLocation = !string.Equals(previousRepository, resolution.Repository, StringComparison.OrdinalIgnoreCase)
                || item.GitHubIssue != resolution.Number;
            var locations = item.PreviousGitHubLocations.ToList();
            if (changedLocation && !locations.Any(location => location.Issue == item.GitHubIssue
                && string.Equals(location.Repository, previousRepository, StringComparison.OrdinalIgnoreCase)))
            {
                locations.Add(new(previousRepository, item.GitHubIssue.GetValueOrDefault()));
            }

            var nodeIds = item.PreviousGitHubNodeIds.Concat(new[] { item.GitHubNodeId, resolution.PreviousNodeId })
                .Where(id => id is not null && !string.Equals(id, resolution.NodeId, StringComparison.Ordinal))
                .Select(id => id!).Distinct(StringComparer.Ordinal).ToArray();
            var updated = item with
            {
                GitHubIssue = resolution.Number,
                GitHubRepository = changedLocation || item.GitHubRepository is not null ? resolution.Repository : null,
                GitHubNodeId = resolution.NodeId ?? item.GitHubNodeId,
                PreviousGitHubLocations = locations,
                PreviousGitHubNodeIds = nodeIds
            };
            items.Add(updated);
            var path = Path.Combine(project.RootPath, item.Path);
            var original = File.ReadAllText(path);
            var content = UpdateContent(original, updated);
            if (!string.Equals(original, content, StringComparison.Ordinal))
            {
                writes.Add(new(path, content, original));
            }
        }

        var duplicates = items.Where(item => item.GitHubIssue is not null)
            .GroupBy(item => $"{item.GitHubRepository ?? project.GitHubRepository}#{item.GitHubIssue}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicates is not null)
        {
            throw new InvalidOperationException($"Resolved GitHub reference maps to multiple roadmap identities: {duplicates.Key}. Review the duplicate before reconciliation.");
        }

        var duplicateNode = items.Where(item => item.GitHubNodeId is not null)
            .GroupBy(item => item.GitHubNodeId, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateNode is not null)
        {
            throw new InvalidOperationException("Resolved GitHub node identity maps to multiple roadmap items; review the duplicate before reconciliation.");
        }

        return new(project.WithItems(items), writes, diagnostics);
    }

    private static IEnumerable<GitHubIssueLocation> ReadLocations(JsonObject github) => github["previousLocations"]?.AsArray()
        .Select(location => new GitHubIssueLocation(location!["repository"]!.GetValue<string>(), location["issue"]!.GetValue<int>())) ?? [];

    private static string UpdateContent(string original, RoadmapItemSnapshot item)
    {
        var root = JsonNode.Parse(original)!.AsObject();
        var github = root["integrations"]!["github"]!.AsObject();
        var changed = github["issue"]!.GetValue<int>() != item.GitHubIssue
            || !string.Equals(github["repository"]?.GetValue<string>(), item.GitHubRepository, StringComparison.Ordinal)
            || !string.Equals(github["nodeId"]?.GetValue<string>(), item.GitHubNodeId, StringComparison.Ordinal)
            || !ReadLocations(github).SequenceEqual(item.PreviousGitHubLocations)
            || !(github["previousNodeIds"]?.AsArray().Select(id => id!.GetValue<string>()) ?? []).SequenceEqual(item.PreviousGitHubNodeIds, StringComparer.Ordinal);
        if (!changed)
        {
            return original;
        }

        github["issue"] = item.GitHubIssue;
        if (item.GitHubRepository is not null)
        {
            github["repository"] = item.GitHubRepository;
        }

        if (item.GitHubNodeId is not null)
        {
            github["nodeId"] = item.GitHubNodeId;
        }

        if (item.PreviousGitHubLocations.Count > 0)
        {
            github["previousLocations"] = new JsonArray(item.PreviousGitHubLocations
                .Select(location => (JsonNode)new JsonObject { ["repository"] = location.Repository, ["issue"] = location.Issue }).ToArray());
        }

        if (item.PreviousGitHubNodeIds.Count > 0)
        {
            github["previousNodeIds"] = new JsonArray(item.PreviousGitHubNodeIds.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }
}
