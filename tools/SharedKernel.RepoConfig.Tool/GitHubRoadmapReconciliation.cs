using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class GitHubRoadmapReconciliation
{
    private const string ApprovedImpact = "1 plus direct open blockers, capped at impactCap.";
    private const string ApprovedOrder = "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID.";

    private GitHubRoadmapReconciliation(
        string manifestPath,
        string repository,
        IReadOnlyList<int> expectedOpenIssueNumbers,
        IReadOnlyList<int> unmappedOpenIssueNumbers,
        IReadOnlyList<int> childrenOfCanonicalPrimaries,
        IReadOnlyDictionary<int, string> directCanonicalPrimaries,
        IReadOnlyList<GitHubRoadmapBlockerEdge> blockerEdges,
        IReadOnlyDictionary<int, string> closedEndpointStates,
        IReadOnlyDictionary<int, string> closedItemTransitions,
        string? snapshotDigest,
        string? ruleVersion,
        GitHubRoadmapPriorityPolicy priorityPolicy,
        string sourceContent)
    {
        ManifestPath = manifestPath;
        Repository = repository;
        ExpectedOpenIssueNumbers = expectedOpenIssueNumbers;
        UnmappedOpenIssueNumbers = unmappedOpenIssueNumbers;
        ChildrenOfCanonicalPrimaries = childrenOfCanonicalPrimaries;
        DirectCanonicalPrimaries = directCanonicalPrimaries;
        BlockerEdges = blockerEdges;
        ClosedEndpointStates = closedEndpointStates;
        ClosedItemTransitions = closedItemTransitions;
        SnapshotDigest = snapshotDigest;
        RuleVersion = ruleVersion;
        PriorityPolicy = priorityPolicy;
        SourceContent = sourceContent;
    }

    public IReadOnlyList<GitHubRoadmapBlockerEdge> BlockerEdges { get; }

    public IReadOnlyDictionary<int, string> ClosedEndpointStates { get; }

    public IReadOnlyDictionary<int, string> ClosedItemTransitions { get; }

    public IReadOnlyList<int> ChildrenOfCanonicalPrimaries { get; }

    public IReadOnlyDictionary<int, string> DirectCanonicalPrimaries { get; }

    public IReadOnlyList<int> ExpectedOpenIssueNumbers { get; }

    public string ManifestPath { get; }

    public string Repository { get; }

    public string? RuleVersion { get; }

    public string? SnapshotDigest { get; }

    public string SourceContent { get; }

    public GitHubRoadmapPriorityPolicy PriorityPolicy { get; }

    public IReadOnlyList<int> UnmappedOpenIssueNumbers { get; }

    public int[] GetRequiredIssueNumbers(RoadmapProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return project.GetGitHubIssueNumbers()
            .Concat(ClosedEndpointStates.Keys)
            .Concat(ClosedItemTransitions.Keys)
            .Distinct()
            .Order()
            .ToArray();
    }

    public static GitHubRoadmapReconciliation Load(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var manifestPath = FindManifestPath(rootPath);
        return Parse(manifestPath, File.ReadAllText(manifestPath));
    }

    public static GitHubRoadmapReconciliation Parse(string manifestPath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(content);

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest root must be a JSON object.");
        }

        var repository = GetRequiredString(root, "repository");
        var priorityPolicy = ReadMechanicalPriorityOverride(root);
        var directCanonicalPrimaries = ReadDirectCanonicalPrimaries(root);
        var childrenOfCanonicalPrimaries = ReadIssueNumbers(root, "childrenOfCanonicalPrimaries");
        var unmappedStructuralRoots = ReadIssueNumbers(root, "unmappedStructuralRoots");
        var needsHuman = ReadIssueNumbers(root, "needsHuman");
        var expectedOpenIssueNumbers = ReadExpectedOpenIssues(directCanonicalPrimaries.Keys, childrenOfCanonicalPrimaries, unmappedStructuralRoots, needsHuman);
        var expectedOpenIssueSet = new HashSet<int>(expectedOpenIssueNumbers);
        var blockerEdges = ReadBlockerEdges(root, expectedOpenIssueSet, out var closedEndpointStates);
        var closedItemTransitions = ReadClosedItemTransitions(root, closedEndpointStates);
        VerifyIntegrity(root, expectedOpenIssueNumbers, directCanonicalPrimaries, childrenOfCanonicalPrimaries, unmappedStructuralRoots, needsHuman, blockerEdges);

        return new GitHubRoadmapReconciliation(
            manifestPath,
            repository,
            expectedOpenIssueNumbers,
            unmappedStructuralRoots.Concat(needsHuman).Order().ToArray(),
            childrenOfCanonicalPrimaries,
            directCanonicalPrimaries,
            blockerEdges,
            closedEndpointStates,
            closedItemTransitions,
            GetNullableString(root, "snapshotDigest"),
            GetNullableString(root, "ruleVersion"),
            priorityPolicy,
            content);
    }

    private static string FindManifestPath(string rootPath)
    {
        var reconciliationPath = Path.Combine(rootPath, RepoConfigPaths.Reconciliation);
        if (!Directory.Exists(reconciliationPath))
        {
            throw new InvalidOperationException("GitHub intake requires one reconciliation manifest under roadmap/reconciliation.");
        }

        var manifests = Directory.EnumerateFiles(reconciliationPath, "open-issues-*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return manifests.Length switch
        {
            1 => manifests[0],
            0 => throw new InvalidOperationException("GitHub intake requires one reconciliation manifest under roadmap/reconciliation."),
            _ => throw new InvalidOperationException("GitHub intake requires exactly one reconciliation manifest under roadmap/reconciliation.")
        };
    }

    private static GitHubRoadmapPriorityPolicy ReadMechanicalPriorityOverride(JsonElement root)
    {
        var priority = GetRequiredObject(root, "mechanicalPriorityOverride");
        if (!string.Equals(GetRequiredString(priority, "impact"), ApprovedImpact, StringComparison.Ordinal)
            || !string.Equals(GetRequiredString(priority, "order"), ApprovedOrder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the approved mechanical priority policy.");
        }

        var effort = GetRequiredObject(priority, "effort");
        Dictionary<string, decimal> effortByLabel = new(StringComparer.Ordinal)
        {
            ["type: epic"] = GetRequiredPositiveDecimal(effort, "type: epic"),
            ["type: feature"] = GetRequiredPositiveDecimal(effort, "type: feature"),
            ["type: enabler"] = GetRequiredPositiveDecimal(effort, "type: enabler"),
            ["type: docs"] = GetRequiredPositiveDecimal(effort, "type: docs"),
            ["type: chore"] = GetRequiredPositiveDecimal(effort, "type: chore")
        };
        var reach = GetRequiredPositiveDecimal(priority, "reach");
        var confidence = GetRequiredPositiveDecimal(priority, "confidence");
        var impactCap = GetRequiredPositiveDecimal(priority, "impactCap");
        var firstItemNumber = GetRequiredPositiveInteger(priority, "firstItemNumber");
        if (confidence > 1m || impactCap is < 1m or > 5m)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest contains invalid mechanical priority values.");
        }

        return new GitHubRoadmapPriorityPolicy(
            firstItemNumber,
            reach,
            confidence,
            impactCap,
            effortByLabel,
            GetRequiredPositiveDecimal(effort, "default"));
    }

    private static Dictionary<int, string> ReadDirectCanonicalPrimaries(JsonElement root)
    {
        var entries = GetRequiredArray(root, "directCanonicalPrimaries");
        Dictionary<int, string> mappings = [];
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("GitHub reconciliation manifest direct canonical primaries must be objects.");
            }

            var issue = GetRequiredPositiveInteger(entry, "issue");
            var roadmapItem = GetRequiredString(entry, "roadmapItem");
            if (!mappings.TryAdd(issue, roadmapItem) || !itemIds.Add(roadmapItem))
            {
                throw new InvalidOperationException("GitHub reconciliation manifest direct canonical primaries must be unique.");
            }
        }

        return mappings;
    }

    private static int[] ReadIssueNumbers(JsonElement root, string propertyName)
    {
        var values = GetRequiredArray(root, propertyName);
        List<int> issueNumbers = [];
        HashSet<int> seen = [];
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var issueNumber) || issueNumber < 1 || !seen.Add(issueNumber))
            {
                throw new InvalidOperationException($"GitHub reconciliation manifest {propertyName} must contain unique positive issue numbers.");
            }

            issueNumbers.Add(issueNumber);
        }

        return [.. issueNumbers];
    }

    private static int[] ReadExpectedOpenIssues(
        IEnumerable<int> directCanonicalPrimaries,
        int[] childrenOfCanonicalPrimaries,
        int[] unmappedStructuralRoots,
        int[] needsHuman)
    {
        var dispositions = directCanonicalPrimaries.Concat(childrenOfCanonicalPrimaries).Concat(unmappedStructuralRoots).Concat(needsHuman).ToArray();
        var expected = dispositions.ToHashSet();
        if (expected.Count != dispositions.Length)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest dispositions must be disjoint.");
        }

        return expected.Order().ToArray();
    }

    private static GitHubRoadmapBlockerEdge[] ReadBlockerEdges(
        JsonElement root,
        HashSet<int> expectedOpenIssueNumbers,
        out Dictionary<int, string> closedEndpointStates)
    {
        var entries = GetRequiredArray(root, "blockerEdges");
        List<GitHubRoadmapBlockerEdge> edges = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        closedEndpointStates = [];
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("GitHub reconciliation manifest blocker edges must be objects.");
            }

            var blocker = GetRequiredPositiveInteger(entry, "blocker");
            var blockerState = GetRequiredIssueState(entry, "blockerState");
            var blocked = GetRequiredPositiveInteger(entry, "blocked");
            var blockedState = GetRequiredIssueState(entry, "blockedState");
            var edge = new GitHubRoadmapBlockerEdge(blocker, blockerState, blocked, blockedState);
            var edgeKey = $"{blocker}:{blockerState}:{blocked}:{blockedState}";
            if (!seen.Add(edgeKey))
            {
                throw new InvalidOperationException("GitHub reconciliation manifest blocker edges must be unique.");
            }

            VerifyEndpointState(blocker, blockerState, expectedOpenIssueNumbers, closedEndpointStates);
            VerifyEndpointState(blocked, blockedState, expectedOpenIssueNumbers, closedEndpointStates);
            edges.Add(edge);
        }

        return [.. edges];
    }

    private static Dictionary<int, string> ReadClosedItemTransitions(JsonElement root, Dictionary<int, string> closedEndpointStates)
    {
        if (!root.TryGetProperty("closedItemTransitions", out var entries))
        {
            return [];
        }

        if (entries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest closed item transitions must be an array.");
        }

        Dictionary<int, string> transitions = [];
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("GitHub reconciliation manifest closed item transitions must be objects.");
            }

            var issue = GetRequiredPositiveInteger(entry, "issue");
            var roadmapItem = GetRequiredString(entry, "roadmapItem");
            if (closedEndpointStates.TryGetValue(issue, out var existingState)
                && !string.Equals(existingState, "CLOSED", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("GitHub reconciliation manifest closed item transitions must reference closed issues.");
            }

            if (!transitions.TryAdd(issue, roadmapItem) || !itemIds.Add(roadmapItem))
            {
                throw new InvalidOperationException("GitHub reconciliation manifest closed item transitions must map unique issues to unique roadmap items.");
            }

            closedEndpointStates[issue] = "CLOSED";
        }

        return transitions;
    }

    private static void VerifyEndpointState(
        int issueNumber,
        string state,
        HashSet<int> expectedOpenIssueNumbers,
        Dictionary<int, string> closedEndpointStates)
    {
        if (string.Equals(state, "OPEN", StringComparison.Ordinal))
        {
            if (!expectedOpenIssueNumbers.Contains(issueNumber))
            {
                throw new InvalidOperationException("GitHub reconciliation manifest blocker edges must reference snapshot issues when open.");
            }

            return;
        }

        if (closedEndpointStates.TryGetValue(issueNumber, out var existingState) && !string.Equals(existingState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("GitHub reconciliation manifest blocker edge states conflict.");
        }

        closedEndpointStates[issueNumber] = state;
    }

    private static void VerifyIntegrity(
        JsonElement root,
        int[] expectedOpenIssueNumbers,
        Dictionary<int, string> directCanonicalPrimaries,
        int[] childrenOfCanonicalPrimaries,
        int[] unmappedStructuralRoots,
        int[] needsHuman,
        GitHubRoadmapBlockerEdge[] blockerEdges)
    {
        var integrity = GetRequiredObject(root, "integrity");
        if (GetRequiredPositiveOrZeroInteger(integrity, "expectedIssueCount") != expectedOpenIssueNumbers.Length
            || GetRequiredPositiveOrZeroInteger(integrity, "expectedDirectCanonicalPrimaryCount") != directCanonicalPrimaries.Count
            || GetRequiredPositiveOrZeroInteger(integrity, "expectedChildrenOfCanonicalPrimaryCount") != childrenOfCanonicalPrimaries.Length
            || GetRequiredPositiveOrZeroInteger(integrity, "expectedUnmappedStructuralRootCount") != unmappedStructuralRoots.Length
            || GetRequiredPositiveOrZeroInteger(integrity, "expectedNeedsHumanCount") != needsHuman.Length
            || GetRequiredPositiveOrZeroInteger(integrity, "expectedBlockerEdgeCount") != blockerEdges.Length
            || !GetRequiredBoolean(integrity, "dispositionsAreDisjoint")
            || !GetRequiredBoolean(integrity, "dispositionsCoverSnapshot"))
        {
            throw new InvalidOperationException("GitHub reconciliation manifest integrity metadata does not match its snapshot.");
        }
    }

    private static JsonElement GetRequiredObject(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the expected metadata.");
        }

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the expected metadata.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the expected metadata.");
        }

        return property.GetString() ?? string.Empty;
    }

    private static string GetRequiredIssueState(JsonElement root, string propertyName)
    {
        var state = GetRequiredString(root, propertyName);
        return state is "OPEN" or "CLOSED"
            ? state
            : throw new InvalidOperationException("GitHub reconciliation manifest blocker edge states must be OPEN or CLOSED.");
    }

    private static string? GetNullableString(JsonElement root, string propertyName) => root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
        ? property.GetString()
        : null;

    private static int GetRequiredPositiveInteger(JsonElement root, string propertyName)
    {
        var value = GetRequiredPositiveOrZeroInteger(root, propertyName);
        return value > 0
            ? value
            : throw new InvalidOperationException("GitHub reconciliation manifest issue numbers must be positive.");
    }

    private static int GetRequiredPositiveOrZeroInteger(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var value)
            || value < 0)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the expected metadata.");
        }

        return value;
    }

    private static decimal GetRequiredDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetDecimal(out var value))
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the expected metadata.");
        }

        return value;
    }

    private static decimal GetRequiredPositiveDecimal(JsonElement root, string propertyName)
    {
        var value = GetRequiredDecimal(root, propertyName);
        return value > 0m
            ? value
            : throw new InvalidOperationException("GitHub reconciliation manifest mechanical priority values must be positive.");
    }

    private static bool GetRequiredBoolean(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException("GitHub reconciliation manifest does not contain the expected metadata.");
        }

        return property.GetBoolean();
    }
}
