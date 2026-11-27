using System.Text.Json;
using SharedKernel.InputNormalization;
using SharedKernel.IO;

namespace SharedKernel.Repositories;

/// <summary>Reads and verifies a forge-backed roadmap with repository-owned priorities.</summary>
public static class ForgeRoadmapReader
{
    private const string PrioritiesPath = "roadmap/priorities.json";
    private const string Untriaged = "untriaged";

    /// <summary>Loads open and explicitly prioritized issues, using local order and RICE inputs.</summary>
    /// <param name="rootPath">The root containing <c>roadmap/priorities.json</c>.</param>
    /// <param name="repository">The primary forge repository in <c>owner/name</c> form.</param>
    /// <param name="reader">The read-only issue provider.</param>
    /// <param name="cancellationToken">Cancels reading the file and issues.</param>
    /// <returns>The roadmap projection and its verification diagnostics.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPath"/> or <paramref name="repository"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static Task<ForgeRoadmapSnapshot> Load(
        string rootPath, string repository, IForgeIssueReader reader, CancellationToken cancellationToken) =>
        Load(rootPath, repository, reader, issueKey: null, cancellationToken);

    /// <summary>Loads the roadmap and optionally includes a directly requested issue, even if it is closed.</summary>
    /// <param name="rootPath">The root containing <c>roadmap/priorities.json</c>.</param>
    /// <param name="repository">The primary forge repository in <c>owner/name</c> form.</param>
    /// <param name="reader">The read-only issue provider.</param>
    /// <param name="issueKey">The requested issue in <c>owner/name#number</c> form, or null.</param>
    /// <param name="cancellationToken">Cancels reading the file and issues.</param>
    /// <returns>The roadmap projection and its verification diagnostics.</returns>
    /// <exception cref="ArgumentException">The root or repository is blank, or the requested issue key is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static async Task<ForgeRoadmapSnapshot> Load(
        string rootPath, string repository, IForgeIssueReader reader, string? issueKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentNullException.ThrowIfNull(reader);
        var requested = issueKey is null ? null : ForgeRoadmapPriority.ParseReference(issueKey)
            ?? throw new ArgumentException("Use a forge issue key in owner/name#number form.", nameof(issueKey));
        List<VerificationIssue> issues = [];
        var priorities = await ReadPriorities(rootPath, issues, cancellationToken).ConfigureAwait(false);
        var references = requested is null ? priorities.Keys : priorities.Keys.Append(ForgeRoadmapPriority.Key(requested));
        return await Read(repository, reader, priorities, references, issues, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a projection against loaded priorities, including any explicit issue references supplied by the caller.</summary>
    internal static async Task<ForgeRoadmapSnapshot> Read(
        string repository, IForgeIssueReader reader, Dictionary<string, ForgeRoadmapPriority> priorities,
        IEnumerable<string> references, List<VerificationIssue> issues, CancellationToken cancellationToken)
    {
        var hasParents = reader.Capabilities.HasFlag(ForgeIssueCapabilities.Parent);
        var hasBlockers = reader.Capabilities.HasFlag(ForgeIssueCapabilities.Blockers);
        if (issues.Count > 0)
        {
            return new ForgeRoadmapSnapshot([], [], issues, hasBlockers);
        }

        Dictionary<string, ForgeIssue> issuesByKey = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var open = await reader.ReadOpenIssues(repository, cancellationToken).ConfigureAwait(false);
            foreach (var item in open)
            {
                if (!item.IsOpen || !StringComparer.OrdinalIgnoreCase.Equals(item.Reference.Repository, repository)
                    || !issuesByKey.TryAdd(ForgeRoadmapPriority.Key(item.Reference), item))
                {
                    issues.Add(new VerificationIssue(PrioritiesPath, "The forge reported inconsistent open issue metadata."));
                    return new ForgeRoadmapSnapshot([], [], issues, hasBlockers);
                }
            }
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            issues.Add(new VerificationIssue(PrioritiesPath, "Open issues could not be read for " + ControlCharacters.Escape(repository) + "."));
            return new ForgeRoadmapSnapshot([], [], issues, hasBlockers);
        }

        HashSet<string> unreadable = new(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            await Resolve(ForgeRoadmapPriority.ParseReference(reference)!).ConfigureAwait(false);
        }

        var work = issuesByKey.Values.OrderBy(static item => ForgeRoadmapPriority.Key(item.Reference), StringComparer.Ordinal).ToArray();
        foreach (var item in work)
        {
            var key = ForgeRoadmapPriority.Key(item.Reference);
            var priority = priorities.GetValueOrDefault(key);
            if (item.IsOpen && IsTriaged(item) && priority?.Order is null)
            {
                issues.Add(new VerificationIssue(PrioritiesPath, $"Open issue {ControlCharacters.Escape(key)} must have an order or the untriaged label."));
            }

            var parent = hasParents ? item.Parent?.Reference : priority?.Parent;
            if (parent is not null)
            {
                await Resolve(parent).ConfigureAwait(false);
            }

            if (hasBlockers)
            {
                foreach (var blocker in item.BlockedBy)
                {
                    await Resolve(blocker.Reference).ConfigureAwait(false);
                }
            }
        }

        return new ForgeRoadmapSnapshot(
            [.. work.Select(Map)], [.. issuesByKey.Values.Select(Map)], issues.AsReadOnly(), hasBlockers);

        async Task Resolve(ForgeIssueReference reference)
        {
            var key = ForgeRoadmapPriority.Key(reference);
            if (issuesByKey.ContainsKey(key) || unreadable.Contains(key))
            {
                return;
            }

            try
            {
                var resolved = await reader.ReadIssue(reference, cancellationToken).ConfigureAwait(false);
                if (StringComparer.OrdinalIgnoreCase.Equals(key, ForgeRoadmapPriority.Key(resolved.Reference)))
                {
                    issuesByKey.Add(key, resolved);
                    return;
                }
            }
            catch (Exception exception) when (IsReadFailure(exception))
            {
                // Report the requested identity without provider response details.
            }

            unreadable.Add(key);
            issues.Add(new VerificationIssue(PrioritiesPath, $"Issue {ControlCharacters.Escape(key)} could not be read."));
        }

        RoadmapItemSnapshot Map(ForgeIssue item)
        {
            var key = ForgeRoadmapPriority.Key(item.Reference);
            var priority = priorities.GetValueOrDefault(key);
            var parent = hasParents ? item.Parent?.Reference : priority?.Parent;
            var triaged = IsTriaged(item);
            return new RoadmapItemSnapshot(key, PrioritiesPath, item.Title, "issue",
                item.IsOpen ? "open" : "closed", string.Empty, triaged,
                triaged ? priority?.Order : null, parent is null ? null : ForgeRoadmapPriority.Key(parent),
                priority?.Reach, priority?.Impact, priority?.Confidence, priority?.Effort,
                hasBlockers ? [.. item.BlockedBy.Select(static link => ForgeRoadmapPriority.Key(link.Reference))] : [],
                [], [], [], item.Labels, null, false);
        }
    }

    private static async Task<Dictionary<string, ForgeRoadmapPriority>> ReadPriorities(
        string rootPath, List<VerificationIssue> issues, CancellationToken cancellationToken)
    {
        Dictionary<string, ForgeRoadmapPriority> priorities = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var content = await BoundedReader.ReadFile(
                RepositoryPath.Parse(PrioritiesPath).ResolveUnder(rootPath), 1_048_576, cancellationToken).ConfigureAwait(false);
            if (content is null)
            {
                issues.Add(new VerificationIssue(PrioritiesPath, "The priorities file exceeds one MiB."));
                return priorities;
            }

            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new VerificationIssue(PrioritiesPath, "The priorities file must be an object keyed by forge issue reference."));
                return priorities;
            }

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                var reference = ForgeRoadmapPriority.ParseReference(entry.Name);
                var priority = ForgeRoadmapPriority.Parse(entry.Value);
                if (reference is null || priority is null
                    || !priorities.TryAdd(ForgeRoadmapPriority.Key(reference), priority))
                {
                    issues.Add(new VerificationIssue(PrioritiesPath, $"Invalid or duplicate priority entry {ControlCharacters.Escape(entry.Name)}."));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            issues.Add(new VerificationIssue(PrioritiesPath, "The priorities file is missing, unreadable, or invalid JSON."));
        }

        return priorities;
    }

    private static bool IsTriaged(ForgeIssue issue) =>
        !issue.Labels.Contains(Untriaged, StringComparer.OrdinalIgnoreCase);

    private static bool IsReadFailure(Exception exception) =>
        exception is InvalidOperationException or TimeoutException or HttpRequestException or ArgumentException;
}
