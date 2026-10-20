using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

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
        string? gitHubRepository,
        bool gitHubEnabled,
        GitHubProjectTarget? gitHubProjectTarget)
    {
        RootPath = rootPath;
        Items = items;
        AllowedStatuses = allowedStatuses;
        ClosedStatuses = closedStatuses;
        ItemIdPrefix = itemIdPrefix;
        GitHubIntakeTheme = gitHubIntakeTheme;
        GitHubIntakeOpenStatus = gitHubIntakeOpenStatus;
        GitHubIntakeClosedStatus = gitHubIntakeClosedStatus;
        GitHubRepository = gitHubRepository;
        GitHubEnabled = gitHubEnabled;
        GitHubProjectTarget = gitHubProjectTarget;
        _itemsById = Items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<RoadmapItemSnapshot> Items { get; }

    public string RootPath { get; }

    public IReadOnlySet<string> ClosedStatuses { get; }

    public IReadOnlySet<string> AllowedStatuses { get; }

    public string? GitHubRepository { get; }

    public bool GitHubEnabled { get; }

    public string ItemIdPrefix { get; }

    public string? GitHubIntakeTheme { get; }

    public string? GitHubIntakeOpenStatus { get; }

    public string? GitHubIntakeClosedStatus { get; }

    public GitHubProjectTarget? GitHubProjectTarget { get; }

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
        var (gitHubIntakeTheme, gitHubIntakeOpenStatus, gitHubIntakeClosedStatus) = ReadGitHubIntakeSettings(config.RootElement);
        return new RoadmapProject(
            rootPath,
            items,
            new HashSet<string>(allowedStatuses, StringComparer.Ordinal),
            new HashSet<string>(closedStatuses, StringComparer.Ordinal),
            itemIdPrefix,
            gitHubIntakeTheme,
            gitHubIntakeOpenStatus,
            gitHubIntakeClosedStatus,
            gitHubRepository,
            gitHubEnabled,
            gitHubProjectTarget);
    }

    public IReadOnlyList<RoadmapItemSnapshot> OpenItems(string? type = null) =>
        Items.Where(item => item.IsTriaged && !IsClosed(item) && (type is null || string.Equals(item.Type, type, StringComparison.Ordinal))).ToArray();

    public bool IsClosed(RoadmapItemSnapshot item) =>
        ClosedStatuses.Contains(item.Status);

    public bool IsUnblocked(RoadmapItemSnapshot item) =>
        BlockersOf(item.Id).All(IsClosed);

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

    public IEnumerable<KeyValuePair<string, int>> TagCounts() =>
        Items.SelectMany(item => item.Tags).GroupBy(tag => tag, StringComparer.Ordinal).Select(group => new KeyValuePair<string, int>(group.Key, group.Count())).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<string, int>> LabelCounts() =>
        Items.SelectMany(item => item.Labels).GroupBy(label => label, StringComparer.Ordinal).Select(group => new KeyValuePair<string, int>(group.Key, group.Count())).OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal);

    public int[] GetGitHubIssueNumbers() => Items
        .Where(item => item.GitHubIssue is int)
        .Select(item => item.GitHubIssue.GetValueOrDefault())
        .Distinct()
        .Order()
        .ToArray();

    private static RoadmapItemSnapshot[] LoadItems(string rootPath) =>
        Directory.EnumerateFiles(Path.Combine(rootPath, RepoConfigPaths.Items), "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .Select(path => LoadItem(rootPath, path))
            .ToArray();

    private static (string? Theme, string? OpenStatus, string? ClosedStatus) ReadGitHubIntakeSettings(JsonElement root)
    {
        if (!root.TryGetProperty("integrations", out var integrations)
            || !integrations.TryGetProperty("github", out var github)
            || !github.TryGetProperty("intake", out var intake)
            || intake.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        return (
            ReadNullableString(intake, "theme"),
            ReadNullableString(intake, "openStatus"),
            ReadNullableString(intake, "closedStatus"));
    }

    private static RoadmapItemSnapshot LoadItem(string rootPath, string itemPath)
    {
        var relativePath = RepoConfigPaths.RelativeTo(rootPath, itemPath);
        using var document = JsonDocument.Parse(File.ReadAllText(itemPath));
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

        return new RoadmapItemSnapshot(
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
            createGitHubIssue);
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
