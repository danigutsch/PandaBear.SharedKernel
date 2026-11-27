using System.Buffers;
using System.Text;
using System.Text.Json;

namespace SharedKernel.Repositories;

/// <summary>Previews migration from local roadmap items to forge issue metadata and reviewed priority inputs.</summary>
public static class ForgeRoadmapMigration
{
    /// <summary>Produces candidate priorities and reports parent, blocker, and query differences.</summary>
    /// <param name="rootPath">The root containing the file-backed roadmap.</param>
    /// <param name="repository">The primary forge repository in <c>owner/name</c> form.</param>
    /// <param name="reader">The read-only issue provider.</param>
    /// <param name="cancellationToken">Cancels the forge reads.</param>
    /// <returns>Candidate JSON and migration diagnostics. An invalid source mapping produces no candidate.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> or <paramref name="repository"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The file-backed roadmap is invalid.</exception>
    public static async Task<(string PrioritiesJson, IReadOnlyList<VerificationIssue> Issues)> Preview(
        string rootPath, string repository, IForgeIssueReader reader, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(reader);
        var project = RoadmapProject.Load(rootPath);
        var openItems = project.Items.Where(item => !project.IsClosed(item)).ToArray();
        List<VerificationIssue> issues = [];
        var references = ReadReferences(project, repository, openItems, issues);
        if (issues.Count != 0)
        {
            return (string.Empty, issues.AsReadOnly());
        }

        var hasParents = reader.Capabilities.HasFlag(ForgeIssueCapabilities.Parent);
        var hasBlockers = reader.Capabilities.HasFlag(ForgeIssueCapabilities.Blockers);
        Dictionary<string, ForgeRoadmapPriority> priorities = new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in openItems)
        {
            var reference = references[item.Id];
            var parent = !hasParents && item.Parent is { } parentId ? references[parentId] : null;
            if (item.Order is not null || parent is not null
                || !StringComparer.OrdinalIgnoreCase.Equals(reference.Repository, repository))
            {
                priorities.Add(ForgeRoadmapPriority.Key(reference),
                    new ForgeRoadmapPriority(item.Order, parent, item.Reach, item.Impact, item.Confidence, item.Effort));
            }
        }

        var keys = references.Values.Select(ForgeRoadmapPriority.Key).Order(StringComparer.OrdinalIgnoreCase);
        var snapshot = await ForgeRoadmapReader.Read(repository, reader, priorities, keys, [], cancellationToken).ConfigureAwait(false);
        issues.AddRange(snapshot.Issues);
        CompareRelationships(openItems, references, snapshot, hasParents, hasBlockers, issues);
        if (snapshot.Issues.Count == 0)
        {
            CompareQueries(project, references, snapshot, hasBlockers, issues);
        }

        return (WritePriorities(priorities.Where(entry => snapshot.FindItem(entry.Key)?.Status != "closed")), issues.AsReadOnly());
    }

    private static Dictionary<string, ForgeIssueReference> ReadReferences(
        RoadmapProject project, string repository, RoadmapItemSnapshot[] openItems, List<VerificationIssue> issues)
    {
        var linkedIds = openItems.SelectMany(item => item.Parent is null ? item.BlockedBy : item.BlockedBy.Append(item.Parent))
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ForgeIssueReference> references = new(StringComparer.Ordinal);
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        foreach (var item in project.Items.Where(item => !project.IsClosed(item) || linkedIds.Contains(item.Id)))
        {
            var issueRepository = item.GitHubRepository ?? repository;
            var reference = item.GitHubIssue is int number && !item.GitHubReferenceUnresolved
                ? ForgeRoadmapPriority.ParseReference(FormattableString.Invariant($"{issueRepository}#{number}"))
                : null;
            if (reference is null)
            {
                issues.Add(new VerificationIssue(item.Path, "The roadmap item needs a resolved forge issue reference."));
            }
            else if (!keys.Add(ForgeRoadmapPriority.Key(reference)))
            {
                issues.Add(new VerificationIssue(item.Path, $"Multiple roadmap items map to {ForgeRoadmapPriority.Key(reference)}."));
            }
            else
            {
                references.Add(item.Id, reference);
            }
        }

        return references;
    }

    private static void CompareRelationships(
        IEnumerable<RoadmapItemSnapshot> localItems, Dictionary<string, ForgeIssueReference> references,
        ForgeRoadmapSnapshot snapshot, bool hasParents, bool hasBlockers, List<VerificationIssue> issues)
    {
        if (!hasBlockers)
        {
            issues.Add(new VerificationIssue(RoadmapPaths.Order, "The forge does not supply blocker data for migration comparison."));
        }

        foreach (var local in localItems)
        {
            var key = ForgeRoadmapPriority.Key(references[local.Id]);
            var remote = snapshot.FindItem(key);
            if (remote is null)
            {
                continue;
            }

            if (remote.Status == "closed")
            {
                issues.Add(new VerificationIssue(local.Path, $"Local open issue {key} is closed on the forge."));
                continue;
            }

            if (hasParents)
            {
                string[] expectedParent = local.Parent is { } parentId ? [ForgeRoadmapPriority.Key(references[parentId])] : [];
                string[] actualParent = remote.Parent is { } remoteParent ? [remoteParent] : [];
                CompareLinks(local.Path, "parent", expectedParent, actualParent, snapshot, issues);
            }

            if (hasBlockers)
            {
                var expectedBlockers = local.BlockedBy.Select(id => ForgeRoadmapPriority.Key(references[id]));
                CompareLinks(local.Path, "blocker", expectedBlockers, remote.BlockedBy, snapshot, issues);
            }
        }
    }

    private static void CompareLinks(
        string path, string relation, IEnumerable<string> expected, IReadOnlyList<string> actual,
        ForgeRoadmapSnapshot snapshot, List<VerificationIssue> issues)
    {
        var local = expected.ToArray();
        if (local.Concat(actual).Any(key => snapshot.FindItem(key) is null))
        {
            return;
        }

        foreach (var key in local.Except(actual, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new VerificationIssue(path, $"Local-only {relation} {key}."));
        }

        foreach (var key in actual.Except(local, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new VerificationIssue(path, $"Forge-only {relation} {key}."));
        }
    }

    private static void CompareQueries(
        RoadmapProject project, Dictionary<string, ForgeIssueReference> references,
        ForgeRoadmapSnapshot snapshot, bool hasBlockers, List<VerificationIssue> issues)
    {
        var open = project.OpenItems().OrderByPriority().ToArray();
        Compare("next-priority", open, snapshot.OpenItems());
        if (hasBlockers)
        {
            var unblocked = open.Where(project.IsUnblocked).ToArray();
            var blockers = open.SelectMany(item => item.BlockedBy).ToHashSet(StringComparer.Ordinal);
            Compare("next-unblocked", unblocked, snapshot.OpenItems(unblockedOnly: true));
            Compare("next-work", unblocked.OrderByDescending(item => blockers.Contains(item.Id)).ToArray(), snapshot.NextWork());
        }

        void Compare(string query, IReadOnlyList<RoadmapItemSnapshot> expected, IReadOnlyList<RoadmapItemSnapshot> actual)
        {
            for (var index = 0; index < Math.Max(expected.Count, actual.Count); index++)
            {
                var local = index < expected.Count ? ForgeRoadmapPriority.Key(references[expected[index].Id]) : "(end)";
                var remote = index < actual.Count ? actual[index].Id : "(end)";
                if (!StringComparer.OrdinalIgnoreCase.Equals(local, remote))
                {
                    issues.Add(new VerificationIssue(RoadmapPaths.Order,
                        FormattableString.Invariant($"{query} differs at position {index + 1}. Local {local}, forge {remote}.")));
                    return;
                }
            }
        }
    }

    private static string WritePriorities(IEnumerable<KeyValuePair<string, ForgeRoadmapPriority>> priorities)
    {
        ArrayBufferWriter<byte> buffer = new();
        using Utf8JsonWriter json = new(buffer, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        foreach (var (key, priority) in priorities.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            json.WriteStartObject(key);
            if (priority.Order is int order)
            {
                json.WriteNumber("order", order);
            }

            if (priority.Parent is { } parent)
            {
                json.WriteString("parent", ForgeRoadmapPriority.Key(parent));
            }

            if (priority.Reach is decimal reach && priority.Impact is decimal impact
                && priority.Confidence is decimal confidence && priority.Effort is decimal effort)
            {
                json.WriteStartObject("scoring");
                json.WriteNumber("reach", reach);
                json.WriteNumber("impact", impact);
                json.WriteNumber("confidence", confidence);
                json.WriteNumber("effort", effort);
                json.WriteEndObject();
            }

            json.WriteEndObject();
        }

        json.WriteEndObject();
        json.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
