using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Loaded repository roadmap: its items, status vocabulary, and GitHub integration settings.</summary>
internal sealed class RoadmapProject
{
    private readonly Dictionary<string, RoadmapItemSnapshot> _itemsById;

    private RoadmapProject(
        string rootPath,
        IReadOnlyList<RoadmapItemSnapshot> items,
        IReadOnlySet<string> allowedStatuses,
        IReadOnlySet<string> closedStatuses,
        string itemIdPrefix,
        string? gitHubIntakeTheme,
        string? gitHubIntakeOpenStatus,
        string? gitHubIntakeClosedStatus,
        string? gitHubIntakeNotPlannedStatus,
        string? gitHubRepository,
        bool gitHubEnabled,
        GitHubProjectTarget? gitHubProjectTarget,
        string? automationPullRequestBody)
    {
        RootPath = rootPath;
        Items = items.Select(item => item with { GitHubReferenceReadOnly = !item.HasLocalGitHubIssue(gitHubRepository) }).ToArray();
        AllowedStatuses = allowedStatuses;
        ClosedStatuses = closedStatuses;
        ItemIdPrefix = itemIdPrefix;
        GitHubIntakeTheme = gitHubIntakeTheme;
        GitHubIntakeOpenStatus = gitHubIntakeOpenStatus;
        GitHubIntakeClosedStatus = gitHubIntakeClosedStatus;
        GitHubIntakeNotPlannedStatus = gitHubIntakeNotPlannedStatus;
        GitHubRepository = gitHubRepository;
        GitHubEnabled = gitHubEnabled;
        GitHubProjectTarget = gitHubProjectTarget;
        AutomationPullRequestBody = automationPullRequestBody;
        _itemsById = Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    /// <summary>All roadmap items in ordinal file-name order.</summary>
    public IReadOnlyList<RoadmapItemSnapshot> Items
    {
        get;
    }

    /// <summary>Repository root the roadmap was loaded from.</summary>
    public string RootPath
    {
        get;
    }

    /// <summary>Statuses that count as closed.</summary>
    public IReadOnlySet<string> ClosedStatuses
    {
        get;
    }

    /// <summary>Statuses an item may use.</summary>
    public IReadOnlySet<string> AllowedStatuses
    {
        get;
    }

    /// <summary>Configured GitHub repository, or null when none is set.</summary>
    public string? GitHubRepository
    {
        get;
    }

    /// <summary>Whether the GitHub integration is enabled.</summary>
    public bool GitHubEnabled
    {
        get;
    }

    /// <summary>Prefix every item id must start with.</summary>
    public string ItemIdPrefix
    {
        get;
    }

    /// <summary>Theme assigned to items imported from GitHub, or null.</summary>
    public string? GitHubIntakeTheme
    {
        get;
    }

    /// <summary>Status given to imported items whose issue is open, or null.</summary>
    public string? GitHubIntakeOpenStatus
    {
        get;
    }

    /// <summary>Status given to imported items whose issue was closed as completed, or null.</summary>
    public string? GitHubIntakeClosedStatus
    {
        get;
    }

    /// <summary>Status given to imported items whose issue was closed as not planned, or null.</summary>
    public string? GitHubIntakeNotPlannedStatus
    {
        get;
    }

    /// <summary>GitHub project the items are synced to, or null.</summary>
    public GitHubProjectTarget? GitHubProjectTarget
    {
        get;
    }

    /// <summary>
    /// The body of the draft pull request that <c>automation publish-result</c> opens, if configured.
    /// </summary>
    public string? AutomationPullRequestBody
    {
        get;
    }

    /// <summary>Verifies the roadmap configuration under <paramref name="rootPath"/> and loads it, throwing when the configuration is invalid.</summary>
    /// <exception cref="InvalidOperationException">The roadmap configuration is invalid or does not define <c>itemIdPrefix</c>.</exception>
    public static RoadmapProject Load(string rootPath)
    {
        var issues = RepoConfigVerifier.Verify(rootPath);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException("Roadmap config is invalid. Run verify before project queries.");
        }

        var configPath = Path.Combine(rootPath, RepoConfigPaths.Config);
        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        var allowedStatuses = ReadStringArray(config.RootElement, "allowed", "statuses");
        var closedStatuses = ReadStringArray(config.RootElement, "project", "closedStatuses");
        var itemIdPrefix = config.RootElement.GetProperty("itemIdPrefix").GetString() ?? throw new InvalidOperationException("roadmap/config.json must define itemIdPrefix.");
        var gitHubRepository = ReadGitHubRepository(config.RootElement);
        var gitHubEnabled = ReadGitHubEnabled(config.RootElement);
        var gitHubProjectTarget = ReadGitHubProjectTarget(config.RootElement);
        var items = LoadItems(rootPath);
        var (gitHubIntakeTheme, gitHubIntakeOpenStatus, gitHubIntakeClosedStatus, gitHubIntakeNotPlannedStatus) = ReadGitHubIntakeSettings(config.RootElement);
        return new RoadmapProject(
            rootPath,
            items,
            new HashSet<string>(allowedStatuses, StringComparer.Ordinal),
            new HashSet<string>(closedStatuses, StringComparer.Ordinal),
            itemIdPrefix,
            gitHubIntakeTheme,
            gitHubIntakeOpenStatus,
            gitHubIntakeClosedStatus,
            gitHubIntakeNotPlannedStatus,
            gitHubRepository,
            gitHubEnabled,
            gitHubProjectTarget,
            ReadAutomationPullRequestBody(config.RootElement));
    }

    /// <summary>Returns triaged items that are not closed, optionally limited to one <paramref name="type"/>.</summary>
    public IReadOnlyList<RoadmapItemSnapshot> OpenItems(string? type = null) =>
        Items.Where(item => item.IsTriaged && !IsClosed(item) && (type is null || string.Equals(item.Type, type, StringComparison.Ordinal))).ToArray();

    /// <summary>Returns whether the item has a closed status.</summary>
    public bool IsClosed(RoadmapItemSnapshot item) =>
        ClosedStatuses.Contains(item.Status);

    /// <summary>Returns whether every blocker of the item is closed.</summary>
    public bool IsUnblocked(RoadmapItemSnapshot item) =>
        BlockersOf(item.Id).All(IsClosed);

    /// <summary>Returns the known items that block <paramref name="itemId"/>, or an empty list when the id is unknown.</summary>
    public IReadOnlyList<RoadmapItemSnapshot> BlockersOf(string itemId)
    {
        if (!_itemsById.TryGetValue(itemId, out var item))
        {
            return [];
        }

        List<RoadmapItemSnapshot> blockers = [];
        foreach (var blockerId in item.BlockedBy)
        {
            if (_itemsById.TryGetValue(blockerId, out var blocker))
            {
                blockers.Add(blocker);
            }
        }

        return blockers;
    }

    /// <summary>Counts how many items use each tag.</summary>
    public IEnumerable<KeyValuePair<string, int>> TagCounts() =>
        Items.SelectMany(item => item.Tags).GroupBy(tag => tag, StringComparer.Ordinal).Select(group => new KeyValuePair<string, int>(group.Key, group.Count())).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal);

    /// <summary>Counts how many items use each label.</summary>
    public IEnumerable<KeyValuePair<string, int>> LabelCounts() =>
        Items.SelectMany(item => item.Labels).GroupBy(label => label, StringComparer.Ordinal).Select(group => new KeyValuePair<string, int>(group.Key, group.Count())).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal);

    /// <summary>Returns a copy of the project with its items replaced by <paramref name="items"/>.</summary>
    public RoadmapProject WithItems(IReadOnlyList<RoadmapItemSnapshot> items) => new(
        RootPath, items, AllowedStatuses, ClosedStatuses, ItemIdPrefix, GitHubIntakeTheme,
        GitHubIntakeOpenStatus, GitHubIntakeClosedStatus, GitHubIntakeNotPlannedStatus, GitHubRepository, GitHubEnabled, GitHubProjectTarget,
        AutomationPullRequestBody);

    /// <summary>Returns the distinct, sorted GitHub issue numbers of items that link an issue in the project repository.</summary>
    public int[] GetGitHubIssueNumbers() => Items
        .Where(item => item.GitHubIssue is int && item.HasLocalGitHubIssue(GitHubRepository))
        .Select(item => item.GitHubIssue.GetValueOrDefault())
        .Distinct()
        .Order()
        .ToArray();

    private static RoadmapItemSnapshot[] LoadItems(string rootPath) =>
        Directory.EnumerateFiles(Path.Combine(rootPath, RepoConfigPaths.Items), "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => LoadItem(rootPath, path))
            .ToArray();

    private static (string? Theme, string? OpenStatus, string? ClosedStatus, string? NotPlannedStatus) ReadGitHubIntakeSettings(JsonElement root)
    {
        if (!root.TryGetProperty("integrations", out var integrations)
            || !integrations.TryGetProperty("github", out var github)
            || !github.TryGetProperty("intake", out var intake)
            || intake.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null, null);
        }

        return (
            ReadNullableString(intake, "theme"),
            ReadNullableString(intake, "openStatus"),
            ReadNullableString(intake, "closedStatus"),
            ReadNullableString(intake, "notPlannedStatus"));
    }

    private static RoadmapItemSnapshot LoadItem(string rootPath, string itemPath) =>
        ReadItemContent(RepoConfigPaths.RelativeTo(rootPath, itemPath), File.ReadAllText(itemPath));

    /// <summary>Parses one roadmap item file's JSON content into a snapshot, treating an item with triage "untriaged" as untriaged.</summary>
    internal static RoadmapItemSnapshot ReadItemContent(string relativePath, string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var isUntriaged = root.TryGetProperty("triage", out var triage)
            && triage.ValueKind == JsonValueKind.String
            && string.Equals(triage.GetString(), "untriaged", StringComparison.Ordinal);
        var isTriaged = !isUntriaged;
        var scoring = root.TryGetProperty("scoring", out var scoringElement) && scoringElement.ValueKind == JsonValueKind.Object
            ? scoringElement
            : default;
        int? order = null;
        decimal? reach = null;
        decimal? impact = null;
        decimal? confidence = null;
        decimal? effort = null;
        if (isTriaged)
        {
            if (root.TryGetProperty("order", out var orderElement) && orderElement.TryGetInt32(out var orderValue))
            {
                order = orderValue;
            }

            reach = ReadDecimal(scoring, "reach");
            impact = ReadDecimal(scoring, "impact");
            confidence = ReadDecimal(scoring, "confidence");
            effort = ReadDecimal(scoring, "effort");
        }

        return GitHubRoadmapItemReference.Read(new RoadmapItemSnapshot(
            root.GetProperty("id").GetString() ?? string.Empty,
            relativePath,
            root.GetProperty("title").GetString() ?? string.Empty,
            root.GetProperty("type").GetString() ?? string.Empty,
            root.GetProperty("status").GetString() ?? string.Empty,
            root.GetProperty("theme").GetString() ?? string.Empty,
            isTriaged,
            order,
            root.TryGetProperty("parent", out var parent) && parent.ValueKind == JsonValueKind.String ? parent.GetString() : null,
            reach,
            impact,
            confidence,
            effort,
            ReadStringArray(root, "blockedBy"),
            ReadStringArray(root, "blocks"),
            ReadStringArray(root, "dependencies"),
            ReadStringArray(root, "tags"),
            ReadStringArray(root, "labels"),
            ReadGitHubIssue(root, out var createGitHubIssue),
            createGitHubIssue), root);
    }

    private static string? ReadGitHubRepository(JsonElement root)
    {
        if (!root.TryGetProperty("integrations", out var integrations)
            || !integrations.TryGetProperty("github", out var github)
            || !github.TryGetProperty("repository", out var repository)
            || repository.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return repository.GetString();
    }

    private static bool ReadGitHubEnabled(JsonElement root)
    {
        if (!root.TryGetProperty("integrations", out var integrations)
            || !integrations.TryGetProperty("github", out var github)
            || !github.TryGetProperty("enabled", out var enabled)
            || enabled.ValueKind != JsonValueKind.True)
        {
            return false;
        }

        return true;
    }

    private static GitHubProjectTarget? ReadGitHubProjectTarget(JsonElement root)
    {
        if (!root.TryGetProperty("integrations", out var integrations)
            || integrations.ValueKind != JsonValueKind.Object
            || !integrations.TryGetProperty("github", out var github)
            || github.ValueKind != JsonValueKind.Object
            || !github.TryGetProperty("projectV2", out var projectV2)
            || projectV2.ValueKind != JsonValueKind.Object
            || !projectV2.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.String
            || !projectV2.TryGetProperty("owner", out var owner)
            || owner.ValueKind != JsonValueKind.String
            || !projectV2.TryGetProperty("number", out var number)
            || number.ValueKind != JsonValueKind.Number
            || !number.TryGetInt32(out var value))
        {
            return null;
        }

        return new GitHubProjectTarget(id.GetString() ?? string.Empty, owner.GetString() ?? string.Empty, value);
    }

    private static string? ReadAutomationPullRequestBody(JsonElement root) =>
        root.TryGetProperty("automation", out var automation) && automation.ValueKind == JsonValueKind.Object
            ? ReadNullableString(automation, "pullRequestBody")
            : null;

    private static int? ReadGitHubIssue(JsonElement root, out bool createRequested)
    {
        createRequested = false;
        if (!root.TryGetProperty("integrations", out var integrations)
            || !integrations.TryGetProperty("github", out var github)
            || !github.TryGetProperty("issue", out var issue))
        {
            return null;
        }

        if (issue.ValueKind == JsonValueKind.String && string.Equals(issue.GetString(), "create", StringComparison.Ordinal))
        {
            createRequested = true;
            return null;
        }

        return issue.ValueKind == JsonValueKind.Number && issue.TryGetInt32(out var value) ? value : null;
    }

    private static decimal? ReadDecimal(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var decimalValue)
            ? decimalValue
            : null;

    private static string? ReadNullableString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string[] ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
            .Select(element => element.GetString() ?? string.Empty)
            .ToArray();
    }

    private static string[] ReadStringArray(JsonElement root, string objectProperty, string arrayProperty)
    {
        if (!root.TryGetProperty(objectProperty, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return ReadStringArray(parent, arrayProperty);
    }
}
