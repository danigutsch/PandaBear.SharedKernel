using System.Text;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class GitHubRoadmapReconciler
{
    private const string RequiredInputs = "GitHub reconciliation requires one seed manifest under roadmap/reconciliation/open-issues-*.json. Required reviewed inputs: mechanicalPriorityOverride, directCanonicalPrimaries, and closedItemTransitions.";
    private const string Source = "GitHub GraphQL repository.issues snapshot with parent, subissue, label, and blocker metadata.";

    private readonly HttpClient? _httpClient;
    private RoadmapProject _project;

    internal GitHubRoadmapReconciler(RoadmapProject project, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(project);

        _project = project;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<string>> Preview(CancellationToken cancellationToken)
    {
        var plan = await CreatePlan(cancellationToken).ConfigureAwait(false);
        return plan.Messages(dryRun: true);
    }

    public async Task<IReadOnlyList<string>> Apply(CancellationToken cancellationToken)
    {
        var plan = await CreatePlan(cancellationToken).ConfigureAwait(false);
        plan.Apply();
        return plan.Messages(dryRun: false);
    }

    private async Task<ReconcilePlan> CreatePlan(CancellationToken cancellationToken)
    {
        var inputSnapshot = RoadmapWriteInputSnapshot.Capture(_project.RootPath);
        _project = RoadmapProject.Load(_project.RootPath);
        inputSnapshot.Verify();
        var repository = GetGitHubRepository();
        var seed = await LoadSeed(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(seed.Reconciliation.Repository, repository, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("GitHub reconciliation manifest repository does not match roadmap/config.json.");
        }

        using var ownedHttpClient = _httpClient is null ? GitHubHttpClient.Create("reconciliation") : null;
        var httpClient = _httpClient ?? ownedHttpClient;
        var client = httpClient is null
            ? throw new InvalidOperationException("GitHub reconciliation could not create an HTTP client.")
            : new GitHubRoadmapReconcileClient(httpClient);
        var snapshot = await client.ReadSnapshot(repository, seed.Reconciliation.GetRequiredIssueNumbers(_project), cancellationToken).ConfigureAwait(false);
        var currentContent = seed.SourceContent;
        var content = CreateManifestContent(seed, repository, snapshot, seed.RetrievedOn);
        if (!string.Equals(currentContent, content, StringComparison.Ordinal))
        {
            var retrievedOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            content = CreateManifestContent(seed, repository, snapshot, retrievedOn);
        }

        return new ReconcilePlan(_project.RootPath, seed.ManifestPath, content, currentContent, inputSnapshot);
    }

    private string GetGitHubRepository()
    {
        if (!_project.GitHubEnabled)
        {
            throw new InvalidOperationException("GitHub reconciliation is disabled in roadmap/config.json.");
        }

        return string.IsNullOrWhiteSpace(_project.GitHubRepository)
            ? throw new InvalidOperationException("roadmap/config.json must define integrations.github.repository before GitHub reconciliation.")
            : _project.GitHubRepository;
    }

    private async Task<ReconcileSeed> LoadSeed(CancellationToken cancellationToken)
    {
        var reconciliationPath = Path.Combine(_project.RootPath, RepoConfigPaths.Reconciliation);
        var manifests = Directory.Exists(reconciliationPath)
            ? Directory.EnumerateFiles(reconciliationPath, "open-issues-*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).ToArray()
            : [];
        if (manifests.Length != 1)
        {
            throw new InvalidOperationException(RequiredInputs);
        }

        var manifestPath = manifests[0];
        var manifestContent = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(manifestContent);
        var root = document.RootElement;
        if (!root.TryGetProperty("closedItemTransitions", out var closedItemTransitions)
            || closedItemTransitions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(RequiredInputs);
        }

        var reconciliation = GitHubRoadmapReconciliation.Parse(manifestPath, manifestContent);
        return new ReconcileSeed(
            reconciliation,
            manifestPath,
            GetRequiredObject(root, "mechanicalPriorityOverride").Clone(),
            GetRequiredArray(root, "rules").Clone(),
            GetRequiredString(root, "ruleVersion"),
            GetNullableString(root, "repositoryCommit"),
            GetRequiredString(root, "retrievedOn"),
            manifestContent);
    }

    private string CreateManifestContent(ReconcileSeed seed, string repository, GitHubRoadmapReconcileSnapshot snapshot, string retrievedOn)
    {
        var issuesByNumber = snapshot.Issues.ToDictionary(issue => issue.Number);
        ValidateRepositoryRelations(snapshot.Issues, repository, issuesByNumber);
        ValidateClosedTransitions(seed.Reconciliation, issuesByNumber);
        var directCanonicalPrimaries = seed.Reconciliation.DirectCanonicalPrimaries
            .Where(mapping => issuesByNumber.TryGetValue(mapping.Key, out var issue) && IsOpen(issue))
            .OrderBy(mapping => mapping.Key)
            .ToArray();
        var directCanonicalIssueNumbers = directCanonicalPrimaries.Select(mapping => mapping.Key).ToHashSet();
        var openIssues = snapshot.Issues.Where(IsOpen).OrderBy(issue => issue.Number).ToArray();
        var dispositions = DeriveDispositions(openIssues, issuesByNumber, directCanonicalIssueNumbers, repository);
        var blockerEdges = DeriveBlockerEdges(snapshot.Issues, issuesByNumber, repository);
        var repositoryCommit = snapshot.RepositoryCommit ?? seed.RepositoryCommit;
        var digestIssueNumbers = _project.GetGitHubIssueNumbers()
            .Concat(seed.Reconciliation.ClosedItemTransitions.Keys)
            .Concat(blockerEdges.Select(edge => edge.Blocker))
            .Concat(blockerEdges.Select(edge => edge.Blocked))
            .ToHashSet();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("repository", repository);
            writer.WriteString("retrievedOn", retrievedOn);
            if (!string.IsNullOrWhiteSpace(repositoryCommit))
            {
                writer.WriteString("repositoryCommit", repositoryCommit);
            }

            writer.WriteString("source", Source);
            writer.WriteString("ruleVersion", seed.RuleVersion);
            writer.WritePropertyName("mechanicalPriorityOverride");
            seed.MechanicalPriorityOverride.WriteTo(writer);
            writer.WritePropertyName("rules");
            seed.Rules.WriteTo(writer);
            writer.WriteString("snapshotDigest", GitHubIssueSnapshotDigest.ComputeForOpenAndRequiredIssues(snapshot.Issues, digestIssueNumbers, repository));
            WriteBlockerEdges(writer, blockerEdges);
            WriteClosedTransitions(writer, seed.Reconciliation.ClosedItemTransitions);
            WriteDirectCanonicalPrimaries(writer, directCanonicalPrimaries);
            WriteIssueNumbers(writer, "childrenOfCanonicalPrimaries", dispositions.ChildrenOfCanonicalPrimaries);
            WriteIssueNumbers(writer, "unmappedStructuralRoots", dispositions.UnmappedStructuralRoots);
            WriteIssueNumbers(writer, "needsHuman", dispositions.NeedsHuman);
            WriteIntegrity(writer, openIssues.Length, directCanonicalPrimaries.Length, dispositions, blockerEdges.Length);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
    }

    private void ValidateClosedTransitions(GitHubRoadmapReconciliation reconciliation, Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber)
    {
        var itemsByIssue = _project.Items.Where(item => item.GitHubIssue is int).ToDictionary(item => item.GitHubIssue.GetValueOrDefault());
        foreach (var (issueNumber, roadmapItemId) in reconciliation.ClosedItemTransitions)
        {
            if (!issuesByNumber.TryGetValue(issueNumber, out var issue) || !IsClosed(issue)
                || !itemsByIssue.TryGetValue(issueNumber, out var item)
                || !string.Equals(item.Id, roadmapItemId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"GitHub reconciliation requires an exact closed item transition: #{issueNumber} -> {roadmapItemId}.");
            }
        }

        var requiredTransitions = itemsByIssue.Values
            .Where(item => item.GitHubIssue is int issueNumber
                && issuesByNumber.TryGetValue(issueNumber, out var issue)
                && IsClosed(issue)
                && !_project.IsClosed(item)
                && !reconciliation.ClosedItemTransitions.ContainsKey(issueNumber))
            .OrderBy(item => item.GitHubIssue)
            .Select(item => $"#{item.GitHubIssue} -> {item.Id}")
            .ToArray();
        if (requiredTransitions.Length > 0)
        {
            throw new InvalidOperationException($"Add closedItemTransitions to {Path.GetRelativePath(_project.RootPath, reconciliation.ManifestPath)}: {string.Join(", ", requiredTransitions)}.");
        }
    }

    private static ReconcileDispositions DeriveDispositions(
        IReadOnlyList<GitHubRoadmapReconcileIssue> openIssues,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        HashSet<int> directCanonicalIssueNumbers,
        string repository)
    {
        List<int> children = [];
        List<int> structuralRoots = [];
        List<int> needsHuman = [];
        foreach (var issue in openIssues)
        {
            if (directCanonicalIssueNumbers.Contains(issue.Number))
            {
                continue;
            }

            if (ReachesCanonicalPrimary(issue, issuesByNumber, directCanonicalIssueNumbers, repository))
            {
                children.Add(issue.Number);
            }
            else if (issue.Parent is null && IsStructuralRoot(issue, repository))
            {
                structuralRoots.Add(issue.Number);
            }
            else
            {
                needsHuman.Add(issue.Number);
            }
        }

        return new ReconcileDispositions(children, structuralRoots, needsHuman);
    }

    private static bool ReachesCanonicalPrimary(
        GitHubRoadmapReconcileIssue issue,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        HashSet<int> directCanonicalIssueNumbers,
        string repository)
    {
        HashSet<int> seen = [issue.Number];
        var parent = issue.Parent;
        while (parent is not null)
        {
            if (!string.Equals(parent.Repository, repository, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parent.State, "OPEN", StringComparison.Ordinal)
                || !issuesByNumber.TryGetValue(parent.Number, out var parentIssue))
            {
                return false;
            }

            if (!seen.Add(parent.Number))
            {
                throw new InvalidOperationException("GitHub reconciliation parent hierarchy contains a cycle.");
            }

            if (directCanonicalIssueNumbers.Contains(parent.Number))
            {
                return true;
            }

            parent = parentIssue.Parent;
        }

        return false;
    }

    private static bool IsStructuralRoot(GitHubRoadmapReconcileIssue issue, string repository) =>
        issue.Labels.Contains("type: epic", StringComparer.Ordinal)
        || issue.SubIssues.Count(relation => relation.State == "OPEN"
            && string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase)) >= 2;

    internal static GitHubRoadmapBlockerEdge[] DeriveBlockerEdges(
        IReadOnlyList<GitHubRoadmapReconcileIssue> issues,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        string repository)
    {
        if (issues.SelectMany(issue => issue.BlockedBy.Concat(issue.Blocking))
            .Any(relation => !string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("GitHub reconciliation does not support cross-repository blocker metadata.");
        }

        foreach (var relation in issues.SelectMany(issue => issue.BlockedBy.Concat(issue.Blocking)))
        {
            ResolveLocalRelation(relation, repository, issuesByNumber);
        }

        var includedIssueNumbers = issuesByNumber.Keys.ToHashSet();
        var blocking = issues.SelectMany(issue => issue.Blocking
            .Where(relation => includedIssueNumbers.Contains(relation.Number)
                && string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(relation => (Blocker: issue.Number, Blocked: relation.Number))).ToHashSet();
        var blockedBy = issues.SelectMany(issue => issue.BlockedBy
            .Where(relation => includedIssueNumbers.Contains(relation.Number)
                && string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(relation => (Blocker: relation.Number, Blocked: issue.Number))).ToHashSet();
        if (!blocking.SetEquals(blockedBy))
        {
            throw new InvalidOperationException("GitHub reconciliation blocker metadata is inconsistent.");
        }

        return blocking
            .Select(edge => CreateBlockerEdge(edge.Blocker, edge.Blocked, issuesByNumber, repository))
            .OrderBy(edge => edge.Blocker)
            .ThenBy(edge => edge.Blocked)
            .ThenBy(edge => edge.BlockerState, StringComparer.Ordinal)
            .ThenBy(edge => edge.BlockedState, StringComparer.Ordinal)
            .ToArray();
    }

    private static GitHubRoadmapBlockerEdge CreateBlockerEdge(
        int blockerNumber,
        int blockedNumber,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber,
        string repository)
    {
        if (!issuesByNumber.TryGetValue(blockerNumber, out var blocker)
            || !issuesByNumber.TryGetValue(blockedNumber, out var blocked))
        {
            throw new InvalidOperationException("GitHub reconciliation blocker metadata references an inaccessible issue.");
        }

        if (blocker.Blocking.Any(relation => relation.Number == blockedNumber && !string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase))
            || blocked.BlockedBy.Any(relation => relation.Number == blockerNumber && !string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("GitHub reconciliation does not support cross-repository blocker metadata.");
        }

        return new GitHubRoadmapBlockerEdge(blockerNumber, blocker.State, blockedNumber, blocked.State);
    }

    private static void ValidateRepositoryRelations(
        IReadOnlyList<GitHubRoadmapReconcileIssue> issues,
        string repository,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber)
    {
        foreach (var issue in issues)
        {
            if (issue.Parent is { } parentRelation
                && ResolveLocalRelation(parentRelation, repository, issuesByNumber) is { } parent
                && !parent.SubIssues.Any(subIssue => subIssue.Number == issue.Number
                    && string.Equals(subIssue.Repository, repository, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(subIssue.State, issue.State, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("GitHub reconciliation parent metadata is inconsistent.");
            }

            foreach (var subIssue in issue.SubIssues)
            {
                if (ResolveLocalRelation(subIssue, repository, issuesByNumber) is { } child
                    && (child.Parent is null
                        || child.Parent.Number != issue.Number
                        || !string.Equals(child.Parent.Repository, repository, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(child.Parent.State, issue.State, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("GitHub reconciliation subissue metadata is inconsistent.");
                }
            }
        }
    }

    private static GitHubRoadmapReconcileIssue? ResolveLocalRelation(
        GitHubRoadmapReconcileRelation relation,
        string repository,
        Dictionary<int, GitHubRoadmapReconcileIssue> issuesByNumber)
    {
        if (!string.Equals(relation.Repository, repository, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!issuesByNumber.TryGetValue(relation.Number, out var target))
        {
            if (string.Equals(relation.State, "OPEN", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"GitHub reconciliation local open relationship target is missing: #{relation.Number}.");
            }

            return null;
        }

        if (!string.Equals(relation.State, target.State, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"GitHub reconciliation relationship state is inconsistent for issue #{relation.Number}.");
        }

        return target;
    }

    private static void WriteBlockerEdges(Utf8JsonWriter writer, IReadOnlyList<GitHubRoadmapBlockerEdge> blockerEdges)
    {
        writer.WritePropertyName("blockerEdges");
        writer.WriteStartArray();
        foreach (var edge in blockerEdges)
        {
            writer.WriteStartObject();
            writer.WriteNumber("blocker", edge.Blocker);
            writer.WriteString("blockerState", edge.BlockerState);
            writer.WriteNumber("blocked", edge.Blocked);
            writer.WriteString("blockedState", edge.BlockedState);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteClosedTransitions(Utf8JsonWriter writer, IReadOnlyDictionary<int, string> closedItemTransitions)
    {
        writer.WritePropertyName("closedItemTransitions");
        writer.WriteStartArray();
        foreach (var (issueNumber, roadmapItemId) in closedItemTransitions.OrderBy(transition => transition.Key))
        {
            writer.WriteStartObject();
            writer.WriteNumber("issue", issueNumber);
            writer.WriteString("roadmapItem", roadmapItemId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteDirectCanonicalPrimaries(Utf8JsonWriter writer, IReadOnlyList<KeyValuePair<int, string>> directCanonicalPrimaries)
    {
        writer.WritePropertyName("directCanonicalPrimaries");
        writer.WriteStartArray();
        foreach (var (issueNumber, roadmapItemId) in directCanonicalPrimaries)
        {
            writer.WriteStartObject();
            writer.WriteNumber("issue", issueNumber);
            writer.WriteString("roadmapItem", roadmapItemId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteIssueNumbers(Utf8JsonWriter writer, string propertyName, IReadOnlyList<int> issueNumbers)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var issueNumber in issueNumbers.Order())
        {
            writer.WriteNumberValue(issueNumber);
        }

        writer.WriteEndArray();
    }

    private static void WriteIntegrity(Utf8JsonWriter writer, int openIssueCount, int directCanonicalPrimaryCount, ReconcileDispositions dispositions, int blockerEdgeCount)
    {
        writer.WritePropertyName("integrity");
        writer.WriteStartObject();
        writer.WriteNumber("expectedIssueCount", openIssueCount);
        writer.WriteNumber("expectedDirectCanonicalPrimaryCount", directCanonicalPrimaryCount);
        writer.WriteNumber("expectedChildrenOfCanonicalPrimaryCount", dispositions.ChildrenOfCanonicalPrimaries.Count);
        writer.WriteNumber("expectedUnmappedStructuralRootCount", dispositions.UnmappedStructuralRoots.Count);
        writer.WriteNumber("expectedNeedsHumanCount", dispositions.NeedsHuman.Count);
        writer.WriteNumber("expectedBlockerEdgeCount", blockerEdgeCount);
        writer.WriteBoolean("dispositionsAreDisjoint", true);
        writer.WriteBoolean("dispositionsCoverSnapshot", true);
        writer.WriteEndObject();
    }

    private static bool IsOpen(GitHubRoadmapReconcileIssue issue) => string.Equals(issue.State, "OPEN", StringComparison.Ordinal);

    private static bool IsClosed(GitHubRoadmapReconcileIssue issue) => string.Equals(issue.State, "CLOSED", StringComparison.Ordinal);

    private static JsonElement GetRequiredObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(RequiredInputs);
        }

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(RequiredInputs);
        }

        return property;
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException(RequiredInputs);
        }

        return property.GetString() ?? string.Empty;
    }

    private static string? GetNullableString(JsonElement root, string propertyName) => root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;

    private sealed class ReconcilePlan(
        string rootPath,
        string manifestPath,
        string content,
        string expectedContent,
        RoadmapWriteInputSnapshot inputSnapshot)
    {
        private bool Matches => string.Equals(expectedContent, content, StringComparison.Ordinal);

        public void Apply() => RollbackFileWriteBatch.Apply(
            rootPath,
            Matches ? [] : [new AtomicFileWrite(manifestPath, content, expectedContent)],
            inputSnapshot.Verify);

        public IReadOnlyList<string> Messages(bool dryRun)
        {
            if (Matches)
            {
                return [dryRun
                    ? "dry-run: reconciliation manifest already matches the GitHub snapshot."
                    : "reconcile: reconciliation manifest already matches the GitHub snapshot."];
            }

            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), manifestPath);
            return [dryRun
                ? $"dry-run: GitHub reconciliation would update {relativePath}."
                : $"reconcile: updated {relativePath}."];
        }
    }

    private sealed record ReconcileDispositions(
        IReadOnlyList<int> ChildrenOfCanonicalPrimaries,
        IReadOnlyList<int> UnmappedStructuralRoots,
        IReadOnlyList<int> NeedsHuman);

    private sealed record ReconcileSeed(
        GitHubRoadmapReconciliation Reconciliation,
        string ManifestPath,
        JsonElement MechanicalPriorityOverride,
        JsonElement Rules,
        string RuleVersion,
        string? RepositoryCommit,
        string RetrievedOn,
        string SourceContent);
}
