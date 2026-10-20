using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal static class RepoConfigVerifier
{
    public static IReadOnlyList<RepoConfigIssue> Verify(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        List<RepoConfigIssue> issues = [];
        CheckRequiredStructure(rootPath, issues);

        var settings = RoadmapSettings.Default;
        var configPath = Path.Combine(rootPath, RepoConfigPaths.Config);
        if (File.Exists(configPath))
        {
            settings = VerifyConfig(rootPath, configPath, issues);
        }

        VerifyItems(rootPath, settings, issues);
        return issues;
    }

    private static void CheckRequiredStructure(string rootPath, List<RepoConfigIssue> issues)
    {
        foreach (var directory in RepoConfigPaths.RequiredDirectories.Where(directory => !Directory.Exists(Path.Combine(rootPath, directory))))
        {
            issues.Add(new RepoConfigIssue(RepoConfigPaths.Normalize(directory), "Missing required directory."));
        }

        foreach (var file in RepoConfigPaths.RequiredFiles.Where(file => !File.Exists(Path.Combine(rootPath, file))))
        {
            issues.Add(new RepoConfigIssue(RepoConfigPaths.Normalize(file), "Missing required file."));
        }
    }

    private static RoadmapSettings VerifyConfig(string rootPath, string configPath, List<RepoConfigIssue> issues)
    {
        var relativePath = RepoConfigPaths.RelativeTo(rootPath, configPath);
        using var document = TryParseJsonFile(rootPath, configPath, issues);
        if (document is null)
        {
            return RoadmapSettings.Default;
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "Config root must be a JSON object."));
            return RoadmapSettings.Default;
        }

        var configuredItemIdPrefix = GetString(root, "itemIdPrefix");
        var itemIdPrefixIsValid = !string.IsNullOrWhiteSpace(configuredItemIdPrefix)
            && configuredItemIdPrefix.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
        if (string.IsNullOrWhiteSpace(GetString(root, "schemaVersion")))
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required string property: schemaVersion."));
        }

        if (!string.Equals(GetString(root, "sourceOfTruth"), "repository", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "sourceOfTruth must be repository."));
        }

        if (string.IsNullOrWhiteSpace(configuredItemIdPrefix))
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required string property: itemIdPrefix."));
        }
        else if (!itemIdPrefixIsValid)
        {
            issues.Add(new RepoConfigIssue(relativePath, "itemIdPrefix may contain only ASCII letters, digits, underscores, and hyphens."));
        }

        var types = VerifyConfigStringArray(root, "allowed", "types", "allowed.types", relativePath, issues);
        var statuses = VerifyConfigStringArray(root, "allowed", "statuses", "allowed.statuses", relativePath, issues);
        var closedStatuses = VerifyConfigStringArray(root, "project", "closedStatuses", "project.closedStatuses", relativePath, issues);
        VerifyUniqueValues(types, "allowed.types", relativePath, issues);
        VerifyUniqueValues(statuses, "allowed.statuses", relativePath, issues);
        VerifyUniqueValues(closedStatuses, "project.closedStatuses", relativePath, issues);
        var distinctTypes = DistinctValues(types);
        var distinctStatuses = DistinctValues(statuses);
        var distinctClosedStatuses = DistinctValues(closedStatuses);
        if (distinctTypes.Length == 0)
        {
            issues.Add(new RepoConfigIssue(relativePath, "allowed.types must contain at least one type."));
        }

        if (distinctStatuses.Length == 0)
        {
            issues.Add(new RepoConfigIssue(relativePath, "allowed.statuses must contain at least one status."));
        }

        VerifyProjectConfig(root, distinctStatuses, distinctClosedStatuses, relativePath, issues);
        VerifyIntegrationsConfig(root, distinctStatuses, distinctClosedStatuses, relativePath, issues);
        VerifyConfigScoring(root, relativePath, issues);

        return new RoadmapSettings(
            itemIdPrefixIsValid ? configuredItemIdPrefix! : RoadmapSettings.Default.ItemIdPrefix,
            distinctTypes.Length == 0 ? RoadmapSettings.Default.AllowedTypes : distinctTypes,
            distinctStatuses.Length == 0 ? RoadmapSettings.Default.AllowedStatuses : distinctStatuses);
    }

    private static void VerifyProjectConfig(JsonElement root, string[] distinctStatuses, string[] distinctClosedStatuses, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty("project", out var project) || project.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required object property: project."));
            return;
        }

        if (project.TryGetProperty("tagFields", out _))
        {
            issues.Add(new RepoConfigIssue(relativePath, "project.tagFields is not supported."));
        }

        if (!string.Equals(GetString(project, "ordering"), "order", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "project.ordering must be order."));
        }

        if (!string.Equals(GetString(project, "blockedBy"), "blockedBy", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "project.blockedBy must be blockedBy."));
        }

        if (distinctClosedStatuses.Length == 0)
        {
            issues.Add(new RepoConfigIssue(relativePath, "project.closedStatuses must contain at least one status."));
        }

        foreach (var closedStatus in distinctClosedStatuses.Where(closedStatus => !distinctStatuses.Contains(closedStatus, StringComparer.Ordinal)))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"project.closedStatuses contains unknown status: {closedStatus}."));
        }
    }

    private static void VerifyIntegrationsConfig(
        JsonElement root,
        string[] allowedStatuses,
        string[] closedStatuses,
        string relativePath,
        List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty("integrations", out var integrations) || integrations.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required object property: integrations."));
            return;
        }

        VerifyGitHubConfig(integrations, allowedStatuses, closedStatuses, relativePath, issues);
    }

    private static void VerifyConfigScoring(JsonElement root, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty("scoring", out var scoring) || scoring.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required object property: scoring."));
            return;
        }

        if (!string.Equals(GetString(scoring, "model"), "RICE", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "scoring.model must be RICE."));
        }

        if (!string.Equals(GetString(scoring, "formula"), "reach * impact * confidence / effort", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "scoring.formula must be reach * impact * confidence / effort."));
        }
    }

    private static void VerifyItems(string rootPath, RoadmapSettings settings, List<RepoConfigIssue> issues)
    {
        var itemsPath = Path.Combine(rootPath, RepoConfigPaths.Items);
        if (!Directory.Exists(itemsPath))
        {
            return;
        }

        List<RoadmapItemSnapshot> items = [];
        foreach (var itemPath in Directory.EnumerateFiles(itemsPath, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            var snapshot = VerifyItem(rootPath, itemPath, settings, issues);
            if (snapshot is not null)
            {
                items.Add(snapshot);
            }
        }

        var themeIds = VerifyThemes(rootPath, issues);
        VerifyUniqueIds(items, issues);
        VerifyUniqueGitHubIssues(items, issues);
        VerifyItemThemes(items, themeIds, issues);
        VerifyReferences(items, issues);
        VerifyParentCycles(items, issues);
        VerifyBlockerConsistency(items, issues);
        VerifyBlockedByCycles(items, issues);
        VerifyOrderFile(rootPath, items, issues);
    }

    private static RoadmapItemSnapshot? VerifyItem(string rootPath, string itemPath, RoadmapSettings settings, List<RepoConfigIssue> issues)
    {
        var relativePath = RepoConfigPaths.RelativeTo(rootPath, itemPath);
        using var document = TryParseJsonFile(rootPath, itemPath, issues);
        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "Roadmap item root must be a JSON object."));
            return null;
        }

        var id = VerifyRequiredString(root, "id", relativePath, issues);
        var title = VerifyRequiredString(root, "title", relativePath, issues);
        var type = VerifyRequiredString(root, "type", relativePath, issues);
        var status = VerifyRequiredString(root, "status", relativePath, issues);
        var isUntriaged = IsUntriaged(root, relativePath, issues);

        int? order = isUntriaged ? null : VerifyRequiredOrder(root, relativePath, issues);
        var parent = GetParent(root, relativePath, issues);

        var theme = VerifyRequiredString(root, "theme", relativePath, issues);
        VerifyRequiredString(root, "outcome", relativePath, issues);

        if (!string.IsNullOrWhiteSpace(id) && !id.StartsWith(settings.ItemIdPrefix + "-", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"id must start with {settings.ItemIdPrefix}-."));
        }

        if (!string.IsNullOrWhiteSpace(type) && !settings.AllowedTypes.Contains(type, StringComparer.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"Unknown roadmap type: {type}."));
        }

        if (!string.IsNullOrWhiteSpace(status) && !settings.AllowedStatuses.Contains(status, StringComparer.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"Unknown roadmap status: {status}."));
        }

        if (isUntriaged)
        {
            VerifyUntriagedPriorityInputs(root, relativePath, issues);
        }
        else
        {
            VerifyScoring(root, relativePath, issues);
        }
        var blockedBy = VerifyStringArray(root, "blockedBy", relativePath, issues, required: true);
        var blocks = VerifyStringArray(root, "blocks", relativePath, issues, required: true);
        var dependencies = VerifyStringArray(root, "dependencies", relativePath, issues, required: true);
        var tags = VerifyStringArray(root, "tags", relativePath, issues, required: true);
        var labels = VerifyStringArray(root, "labels", relativePath, issues, required: true);
        var githubIssue = GetGitHubIssue(root, relativePath, issues, out var createGitHubIssue);
        decimal? reach = null;
        decimal? impact = null;
        decimal? confidence = null;
        decimal? effort = null;
        if (!isUntriaged)
        {
            reach = GetDecimal(root, "scoring", "reach");
            impact = GetDecimal(root, "scoring", "impact");
            confidence = GetDecimal(root, "scoring", "confidence");
            effort = GetDecimal(root, "scoring", "effort");
        }

        return string.IsNullOrWhiteSpace(id)
            ? null
            : new RoadmapItemSnapshot(
                id,
                relativePath,
                title,
                type,
                status,
                theme,
                !isUntriaged,
                order,
                parent,
                reach,
                impact,
                confidence,
                effort,
                blockedBy,
                blocks,
                dependencies,
                tags,
                labels,
                githubIssue,
                createGitHubIssue);
    }

    private static bool IsUntriaged(JsonElement root, string relativePath, List<RepoConfigIssue> issues)
    {
        var isUntriaged = root.TryGetProperty("triage", out var triage);
        if (isUntriaged && (triage.ValueKind != JsonValueKind.String || !string.Equals(triage.GetString(), "untriaged", StringComparison.Ordinal)))
        {
            issues.Add(new RepoConfigIssue(relativePath, "triage must be untriaged when present."));
            return false;
        }

        return isUntriaged;
    }

    private static string? GetParent(JsonElement root, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty("parent", out var parentElement))
        {
            return null;
        }

        if (parentElement.ValueKind != JsonValueKind.String)
        {
            issues.Add(new RepoConfigIssue(relativePath, "parent must be a string when present."));
            return null;
        }

        var parent = parentElement.GetString();
        if (string.IsNullOrWhiteSpace(parent))
        {
            issues.Add(new RepoConfigIssue(relativePath, "parent must not be blank when present."));
            return null;
        }

        return parent;
    }

    private static void VerifyScoring(JsonElement root, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty("scoring", out var scoring) || scoring.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required object property: scoring."));
            return;
        }

        VerifyNumber(scoring, "reach", relativePath, issues, value => value >= 0, "reach must be 0 or greater.");
        VerifyNumber(scoring, "impact", relativePath, issues, value => value is >= 1 and <= 5, "impact must be between 1 and 5.");
        VerifyNumber(scoring, "confidence", relativePath, issues, value => value is >= 0.1m and <= 1, "confidence must be between 0.1 and 1.0.");
        VerifyNumber(scoring, "effort", relativePath, issues, value => value > 0, "effort must be greater than 0.");
    }

    private static void VerifyUntriagedPriorityInputs(JsonElement root, string relativePath, List<RepoConfigIssue> issues)
    {
        if (root.TryGetProperty("order", out _) || root.TryGetProperty("scoring", out _))
        {
            issues.Add(new RepoConfigIssue(relativePath, "Untriaged roadmap items must not define priority inputs."));
        }
    }

    private static void VerifyNumber(JsonElement parent, string propertyName, string relativePath, List<RepoConfigIssue> issues, Func<decimal, bool> isValid, string failureMessage)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var value))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"Missing required numeric scoring property: {propertyName}."));
            return;
        }

        if (!isValid(value))
        {
            issues.Add(new RepoConfigIssue(relativePath, failureMessage));
        }
    }

    private static int VerifyRequiredOrder(JsonElement root, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty("order", out var orderProperty) || orderProperty.ValueKind != JsonValueKind.Number || !orderProperty.TryGetInt32(out var order))
        {
            issues.Add(new RepoConfigIssue(relativePath, "Missing required integer property: order."));
            return 0;
        }

        if (order < 1)
        {
            issues.Add(new RepoConfigIssue(relativePath, "order must be 1 or greater."));
        }

        return order;
    }

    private static List<string> VerifyStringArray(JsonElement root, string propertyName, string relativePath, List<RepoConfigIssue> issues, bool required)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            if (required)
            {
                issues.Add(new RepoConfigIssue(relativePath, $"{propertyName} must be an array."));
            }

            return [];
        }

        List<string> values = [];
        HashSet<string> uniqueValues = new(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"{propertyName} must contain only non-empty strings."));
                continue;
            }

            var value = item.GetString() ?? string.Empty;
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"{propertyName} must not contain leading or trailing whitespace."));
                continue;
            }

            if (!uniqueValues.Add(value))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"{propertyName} contains a duplicate value: {value}."));
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    private static void VerifyUniqueIds(IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        var duplicates = items.GroupBy(item => item.Id, StringComparer.Ordinal).Where(group => group.Count() > 1);
        foreach (var duplicate in duplicates)
        {
            foreach (var item in duplicate)
            {
                issues.Add(new RepoConfigIssue(item.Path, $"Duplicate roadmap item id: {item.Id}."));
            }
        }
    }

    private static void VerifyUniqueGitHubIssues(IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        var duplicates = items
            .Where(item => item.GitHubIssue is not null)
            .GroupBy(item => item.GitHubIssue)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            foreach (var item in duplicate)
            {
                issues.Add(new RepoConfigIssue(item.Path, $"Duplicate GitHub issue mapping: {duplicate.Key}."));
            }
        }
    }

    private static HashSet<string> VerifyThemes(string rootPath, List<RepoConfigIssue> issues)
    {
        HashSet<string> themeIds = new(StringComparer.Ordinal);
        var themesPath = Path.Combine(rootPath, RepoConfigPaths.Themes);
        if (!Directory.Exists(themesPath))
        {
            return themeIds;
        }

        foreach (var themePath in Directory.EnumerateFiles(themesPath, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            var relativePath = RepoConfigPaths.RelativeTo(rootPath, themePath);
            using var document = TryParseJsonFile(rootPath, themePath, issues);
            if (document is null)
            {
                continue;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new RepoConfigIssue(relativePath, "Theme root must be a JSON object."));
                continue;
            }

            var id = GetString(document.RootElement, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(new RepoConfigIssue(relativePath, "Missing required string property: id."));
                continue;
            }

            if (!themeIds.Add(id))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"Duplicate theme id: {id}."));
            }
        }

        return themeIds;
    }

    private static void VerifyItemThemes(IReadOnlyCollection<RoadmapItemSnapshot> items, HashSet<string> themeIds, List<RepoConfigIssue> issues)
    {
        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Theme) && !themeIds.Contains(item.Theme)))
        {
            issues.Add(new RepoConfigIssue(item.Path, $"Unknown theme: {item.Theme}."));
        }
    }

    private static void VerifyReferences(IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        HashSet<string> ids = new(items.Select(item => item.Id), StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Parent))
            {
                VerifyReference(ids, item, item.Parent, "parent", issues);
            }

            foreach (var dependency in item.Dependencies)
            {
                VerifyReference(ids, item, dependency, "dependency", issues);
            }

            foreach (var blocker in item.BlockedBy)
            {
                VerifyReference(ids, item, blocker, "blocker", issues);
            }

            foreach (var blockedItem in item.Blocks)
            {
                VerifyReference(ids, item, blockedItem, "blocked item", issues);
            }
        }
    }

    private static void VerifyParentCycles(IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        var itemsById = items
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> reported = new(StringComparer.Ordinal);
        foreach (var item in items)
        {
            DetectParentCycle(item, itemsById, [], reported, issues);
        }
    }

    private static void DetectParentCycle(
        RoadmapItemSnapshot item,
        IReadOnlyDictionary<string, RoadmapItemSnapshot> itemsById,
        HashSet<string> path,
        HashSet<string> reported,
        List<RepoConfigIssue> issues)
    {
        if (!path.Add(item.Id))
        {
            if (reported.Add(item.Id))
            {
                issues.Add(new RepoConfigIssue(item.Path, $"parent cycle includes {item.Id}."));
            }

            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(item.Parent)
                && !string.Equals(item.Id, item.Parent, StringComparison.Ordinal)
                && itemsById.TryGetValue(item.Parent, out var parent))
            {
                DetectParentCycle(parent, itemsById, path, reported, issues);
            }
        }
        finally
        {
            path.Remove(item.Id);
        }
    }

    private static void VerifyBlockerConsistency(IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        var itemsById = items
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var item in items)
        {
            foreach (var blockerId in item.BlockedBy.Where(itemsById.ContainsKey))
            {
                var blocker = itemsById[blockerId];
                if (!blocker.Blocks.Contains(item.Id, StringComparer.Ordinal))
                {
                    issues.Add(new RepoConfigIssue(item.Path, $"blockedBy {blockerId} must be reciprocated by {blockerId}.blocks."));
                }
            }

            foreach (var blockedId in item.Blocks.Where(itemsById.ContainsKey))
            {
                var blocked = itemsById[blockedId];
                if (!blocked.BlockedBy.Contains(item.Id, StringComparer.Ordinal))
                {
                    issues.Add(new RepoConfigIssue(item.Path, $"blocks {blockedId} must be reciprocated by {blockedId}.blockedBy."));
                }
            }
        }
    }

    private static void VerifyBlockedByCycles(IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        var itemsById = items
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> reported = new(StringComparer.Ordinal);
        foreach (var item in items)
        {
            DetectCycle(item, itemsById, [], reported, issues);
        }
    }

    private static void DetectCycle(
        RoadmapItemSnapshot item,
        IReadOnlyDictionary<string, RoadmapItemSnapshot> itemsById,
        HashSet<string> path,
        HashSet<string> reported,
        List<RepoConfigIssue> issues)
    {
        if (!path.Add(item.Id))
        {
            ReportBlockedByCycle(item, reported, issues);
            return;
        }

        try
        {
            foreach (var blockerId in item.BlockedBy.Where(itemsById.ContainsKey))
            {
                DetectCycle(itemsById[blockerId], itemsById, path, reported, issues);
            }
        }
        finally
        {
            path.Remove(item.Id);
        }
    }

    private static void ReportBlockedByCycle(RoadmapItemSnapshot item, HashSet<string> reported, List<RepoConfigIssue> issues)
    {
        if (reported.Add(item.Id))
        {
            issues.Add(new RepoConfigIssue(item.Path, $"blockedBy cycle includes {item.Id}."));
        }
    }

    private static void VerifyOrderFile(string rootPath, IReadOnlyCollection<RoadmapItemSnapshot> items, List<RepoConfigIssue> issues)
    {
        var orderPath = Path.Combine(rootPath, RepoConfigPaths.Order);
        if (!File.Exists(orderPath))
        {
            return;
        }

        var relativePath = RepoConfigPaths.RelativeTo(rootPath, orderPath);
        using var document = TryParseJsonFile(rootPath, orderPath, issues);
        if (document is null)
        {
            return;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "order.json root must be a JSON object."));
            return;
        }

        if (!document.RootElement.TryGetProperty("items", out var orderItems) || orderItems.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new RepoConfigIssue(relativePath, "order.json must contain an items array."));
            return;
        }

        HashSet<string> roadmapIds = new(items.Where(item => item.IsTriaged).Select(item => item.Id), StringComparer.Ordinal);
        HashSet<string> untriagedIds = new(items.Where(item => !item.IsTriaged).Select(item => item.Id), StringComparer.Ordinal);
        HashSet<string> orderedIds = new(StringComparer.Ordinal);
        List<string> orderedValues = [];
        VerifyOrderItems(orderItems, roadmapIds, untriagedIds, orderedIds, orderedValues, relativePath, issues);

        foreach (var missingId in roadmapIds.Except(orderedIds, StringComparer.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"Missing ordered item: {missingId}."));
        }

        var expectedOrder = items.Where(item => item.IsTriaged).OrderByPriority().Select(item => item.Id);
        if (!orderedValues.SequenceEqual(expectedOrder, StringComparer.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "order.json items must match priority order: order ascending, score descending, then id."));
        }
    }

    private static void VerifyOrderItems(
        JsonElement orderItems,
        HashSet<string> roadmapIds,
        HashSet<string> untriagedIds,
        HashSet<string> orderedIds,
        List<string> orderedValues,
        string relativePath,
        List<RepoConfigIssue> issues)
    {
        foreach (var orderItem in orderItems.EnumerateArray())
        {
            if (orderItem.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(orderItem.GetString()))
            {
                issues.Add(new RepoConfigIssue(relativePath, "order.json items must contain only non-empty strings."));
                continue;
            }

            var id = orderItem.GetString() ?? string.Empty;
            orderedValues.Add(id);
            if (!orderedIds.Add(id))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"Duplicate ordered item: {id}."));
            }

            if (untriagedIds.Contains(id))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"order.json must not contain untriaged item: {id}."));
            }
            else if (!roadmapIds.Contains(id))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"Unknown ordered item: {id}."));
            }
        }
    }

    private static void VerifyReference(HashSet<string> ids, RoadmapItemSnapshot item, string referencedId, string referenceName, List<RepoConfigIssue> issues)
    {
        if (string.Equals(item.Id, referencedId, StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(item.Path, $"Roadmap item cannot reference itself as {referenceName}."));
            return;
        }

        if (!ids.Contains(referencedId))
        {
            issues.Add(new RepoConfigIssue(item.Path, $"Unknown {referenceName}: {referencedId}."));
        }
    }

    private static string VerifyRequiredString(JsonElement parent, string propertyName, string relativePath, List<RepoConfigIssue> issues)
    {
        var value = GetString(parent, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"Missing required string property: {propertyName}."));
            return string.Empty;
        }

        return value;
    }

    private static string? GetString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static decimal? GetDecimal(JsonElement root, string objectProperty, string propertyName)
    {
        if (!root.TryGetProperty(objectProperty, out var parent)
            || parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDecimal(out var result))
        {
            return null;
        }

        return result;
    }

    private static int? GetGitHubIssue(JsonElement root, string relativePath, List<RepoConfigIssue> issues, out bool createRequested)
    {
        createRequested = false;
        if (!root.TryGetProperty("integrations", out var integrations))
        {
            return null;
        }

        if (integrations.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations must be a JSON object when present."));
            return null;
        }

        if (!integrations.TryGetProperty("github", out var github))
        {
            return null;
        }

        if (github.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github must be a JSON object when present."));
            return null;
        }

        if (github.TryGetProperty("subIssues", out _))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.subIssues is not supported; use parent links."));
        }

        if (!github.TryGetProperty("issue", out var issue))
        {
            return null;
        }

        if (issue.ValueKind == JsonValueKind.String && string.Equals(issue.GetString(), "create", StringComparison.Ordinal))
        {
            createRequested = true;
            return null;
        }

        if (issue.ValueKind != JsonValueKind.Number || !issue.TryGetInt32(out var issueNumber) || issueNumber < 1)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.issue must be a positive integer or exact string create."));
            return null;
        }

        return issueNumber;
    }

    private static void VerifyGitHubConfig(
        JsonElement integrations,
        string[] allowedStatuses,
        string[] closedStatuses,
        string relativePath,
        List<RepoConfigIssue> issues)
    {
        if (!integrations.TryGetProperty("github", out var github))
        {
            return;
        }

        if (github.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github must be a JSON object."));
            return;
        }

        if (github.TryGetProperty("projectFieldMapping", out _))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.projectFieldMapping is not supported."));
        }

        if (github.TryGetProperty("sourceOfTruth", out _) && !string.Equals(GetString(github, "sourceOfTruth"), "projection", StringComparison.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.sourceOfTruth must be projection."));
        }

        if (github.TryGetProperty("enabled", out var enabled) && enabled.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.enabled must be a Boolean."));
        }

        if (github.TryGetProperty("enabled", out enabled) && enabled.ValueKind == JsonValueKind.True)
        {
            var repository = GetString(github, "repository");
            if (string.IsNullOrWhiteSpace(repository) || !GitHubRepositoryName.IsValid(repository))
            {
                issues.Add(new RepoConfigIssue(relativePath, "integrations.github.repository must be shaped as owner/repository when GitHub sync is enabled."));
            }
        }

        VerifyGitHubIntakeConfig(github, allowedStatuses, closedStatuses, relativePath, issues);
        VerifyGitHubProjectTarget(github, relativePath, issues);
    }

    private static void VerifyGitHubIntakeConfig(
        JsonElement github,
        string[] allowedStatuses,
        string[] closedStatuses,
        string relativePath,
        List<RepoConfigIssue> issues)
    {
        if (!github.TryGetProperty("intake", out var intake))
        {
            return;
        }

        if (intake.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.intake must be a JSON object."));
            return;
        }

        var theme = GetString(intake, "theme");
        if (string.IsNullOrWhiteSpace(theme))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.intake.theme must be a non-empty string."));
        }

        var openStatus = GetString(intake, "openStatus");
        if (string.IsNullOrWhiteSpace(openStatus)
            || !allowedStatuses.Contains(openStatus, StringComparer.Ordinal)
            || closedStatuses.Contains(openStatus, StringComparer.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.intake.openStatus must be an allowed non-closed status."));
        }

        var closedStatus = GetString(intake, "closedStatus");
        if (string.IsNullOrWhiteSpace(closedStatus) || !closedStatuses.Contains(closedStatus, StringComparer.Ordinal))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.intake.closedStatus must be a configured closed status."));
        }
    }

    private static void VerifyGitHubProjectTarget(JsonElement github, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!github.TryGetProperty("projectV2", out var projectV2))
        {
            return;
        }

        if (projectV2.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.projectV2 must be a JSON object."));
            return;
        }

        if (!github.TryGetProperty("enabled", out var enabled) || enabled.ValueKind == JsonValueKind.False)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.projectV2 requires GitHub sync to be enabled."));
        }

        var id = GetString(projectV2, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.projectV2.id must be a non-empty string."));
        }

        var owner = GetString(projectV2, "owner");
        if (string.IsNullOrWhiteSpace(owner) || !GitHubRepositoryName.IsValidOwner(owner))
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.projectV2.owner must be a valid GitHub owner."));
        }

        if (!projectV2.TryGetProperty("number", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var value) || value <= 0)
        {
            issues.Add(new RepoConfigIssue(relativePath, "integrations.github.projectV2.number must be a positive integer."));
        }
    }

    private static void VerifyUniqueValues(IReadOnlyCollection<string> values, string propertyPath, string relativePath, List<RepoConfigIssue> issues)
    {
        foreach (var duplicate in values.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            issues.Add(new RepoConfigIssue(relativePath, $"{propertyPath} contains a duplicate value: {duplicate.Key}."));
        }
    }

    private static string[] DistinctValues(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).ToArray();

    private static List<string> VerifyConfigStringArray(JsonElement root, string objectProperty, string arrayProperty, string propertyPath, string relativePath, List<RepoConfigIssue> issues)
    {
        if (!root.TryGetProperty(objectProperty, out var parent)
            || parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(arrayProperty, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> values = [];
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"{propertyPath} must contain only non-empty strings."));
                continue;
            }

            var value = element.GetString() ?? string.Empty;
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                issues.Add(new RepoConfigIssue(relativePath, $"{propertyPath} must not contain leading or trailing whitespace."));
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    internal static JsonDocument? TryParseJsonFile(string rootPath, string path, List<RepoConfigIssue> issues, Func<string, string>? readFile = null)
    {
        try
        {
            return JsonDocument.Parse((readFile ?? File.ReadAllText)(path));
        }
        catch (JsonException exception)
        {
            issues.Add(new RepoConfigIssue(RepoConfigPaths.RelativeTo(rootPath, path), $"Invalid JSON: {exception.Message}"));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new RepoConfigIssue(RepoConfigPaths.RelativeTo(rootPath, path), $"Unable to read JSON: {exception.Message}"));
            return null;
        }
    }
}
