using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PandaBear.RepoConfig.Tool;

/// <summary>Immutable, digest-sealed plan describing one roadmap maintenance run: the GitHub snapshot it was based on and the closed-item transitions it requires.</summary>
internal sealed class RoadmapAutomationPlan
{
    private const int CurrentSchemaVersion = 1;
    private const int MaxPlanBytes = 1024 * 1024;

    private RoadmapAutomationPlan(
        string repository,
        long runId,
        string branchName,
        string baseSha,
        string defaultBranchOid,
        string snapshotDigest,
        string repositoryInputDigest,
        string reconciliationManifest,
        string baselineClosedTransitionsDigest,
        string approvedClosedTransitionsDigest,
        RoadmapClosedItemTransition[] requiredClosedTransitions,
        bool snapshotChanged,
        string planDigest)
    {
        Repository = repository;
        RunId = runId;
        BranchName = branchName;
        BaseSha = baseSha;
        DefaultBranchOid = defaultBranchOid;
        SnapshotDigest = snapshotDigest;
        RepositoryInputDigest = repositoryInputDigest;
        ReconciliationManifest = reconciliationManifest;
        BaselineClosedTransitionsDigest = baselineClosedTransitionsDigest;
        ApprovedClosedTransitionsDigest = approvedClosedTransitionsDigest;
        RequiredClosedTransitions = requiredClosedTransitions;
        SnapshotChanged = snapshotChanged;
        HasChanges = snapshotChanged || requiredClosedTransitions.Length > 0;
        PlanDigest = planDigest;
    }

    /// <summary>Digest of the baseline closed transitions merged with the required ones.</summary>
    public string ApprovedClosedTransitionsDigest
    {
        get;
    }

    /// <summary>Commit SHA of the base the plan was created against.</summary>
    public string BaseSha
    {
        get;
    }

    /// <summary>Digest of the closed transitions already recorded in the reconciliation manifest.</summary>
    public string BaselineClosedTransitionsDigest
    {
        get;
    }

    /// <summary>Name of the branch the run publishes its changes to.</summary>
    public string BranchName
    {
        get;
    }

    /// <summary>Object id of the default branch head when the plan was created.</summary>
    public string DefaultBranchOid
    {
        get;
    }

    /// <summary>Whether the run changes anything, either the snapshot or closed transitions.</summary>
    public bool HasChanges
    {
        get;
    }

    /// <summary>Digest over the whole plan content, used to bind later steps to this plan.</summary>
    public string PlanDigest
    {
        get;
    }

    /// <summary>Repository-relative path of the reconciliation manifest.</summary>
    public string ReconciliationManifest
    {
        get;
    }

    /// <summary>Closed-item transitions that must be added to the manifest, ordered by issue number.</summary>
    public IReadOnlyList<RoadmapClosedItemTransition> RequiredClosedTransitions
    {
        get;
    }

    /// <summary>Owner and name of the GitHub repository the plan targets.</summary>
    public string Repository
    {
        get;
    }

    /// <summary>Digest of the repository files the plan was computed from.</summary>
    public string RepositoryInputDigest
    {
        get;
    }

    /// <summary>Identifier of the workflow run that created the plan.</summary>
    public long RunId
    {
        get;
    }

    /// <summary>Digest of the GitHub roadmap snapshot the plan was computed from.</summary>
    public string SnapshotDigest
    {
        get;
    }

    /// <summary>Whether the GitHub snapshot differs from the repository content.</summary>
    public bool SnapshotChanged
    {
        get;
    }

    /// <summary>Validates the inputs, orders and merges the closed transitions, derives the branch name, and returns the plan sealed with its digest.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baselineClosedTransitions"/> or <paramref name="requiredClosedTransitions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="runId"/> is less than 1.</exception>
    public static RoadmapAutomationPlan Create(
        string repository,
        long runId,
        string baseSha,
        string defaultBranchOid,
        string snapshotDigest,
        string repositoryInputDigest,
        string reconciliationManifest,
        IReadOnlyList<RoadmapClosedItemTransition> baselineClosedTransitions,
        IReadOnlyList<RoadmapClosedItemTransition> requiredClosedTransitions,
        bool snapshotChanged)
    {
        ArgumentNullException.ThrowIfNull(baselineClosedTransitions);
        ArgumentNullException.ThrowIfNull(requiredClosedTransitions);

        var normalizedRepository = ValidateRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(runId, 1);
        var normalizedBaseSha = ValidateSha(baseSha, "base SHA");
        var normalizedDefaultBranchOid = ValidateSha(defaultBranchOid, "default-branch OID");
        var normalizedSnapshotDigest = ValidateDigest(snapshotDigest, "GitHub snapshot digest");
        var normalizedInputDigest = ValidateDigest(repositoryInputDigest, "repository input digest");
        var normalizedManifest = ValidateManifestPath(reconciliationManifest);
        var baseline = baselineClosedTransitions.OrderBy(transition => transition.Issue).ToArray();
        ValidateTransitions(baseline);
        var required = requiredClosedTransitions.OrderBy(transition => transition.Issue).ToArray();
        ValidateTransitions(required);
        var approved = MergeTransitions(baseline, required);
        var baselineDigest = ComputeTransitionsDigest(baseline);
        var approvedDigest = ComputeTransitionsDigest(approved);
        var branchName = CreateBranchName(runId, normalizedBaseSha);
        var unsigned = new RoadmapAutomationPlan(
            normalizedRepository,
            runId,
            branchName,
            normalizedBaseSha,
            normalizedDefaultBranchOid,
            normalizedSnapshotDigest,
            normalizedInputDigest,
            normalizedManifest,
            baselineDigest,
            approvedDigest,
            required,
            snapshotChanged,
            planDigest: string.Empty);
        return unsigned.WithPlanDigest(unsigned.ComputePlanDigest());
    }

    /// <summary>Reads the plan file at <paramref name="path"/>, strictly validates its shape, and returns it only when its digest equals <paramref name="expectedPlanDigest"/>.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The plan run ID is less than 1.</exception>
    /// <exception cref="InvalidOperationException">The plan file is missing, too large, or invalid, or its digest does not match <paramref name="expectedPlanDigest"/>.</exception>
    public static RoadmapAutomationPlan Load(string path, string expectedPlanDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalizedExpectedDigest = ValidateDigest(expectedPlanDigest, "expected plan digest");
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > MaxPlanBytes)
        {
            throw new InvalidOperationException("Roadmap automation plan is missing or exceeds the allowed size.");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        ValidateObjectProperties(
            root,
            [
                "schemaVersion",
                "repository",
                "runId",
                "branchName",
                "baseSha",
                "defaultBranchOid",
                "snapshotDigest",
                "repositoryInputDigest",
                "reconciliationManifest",
                "baselineClosedTransitionsDigest",
                "approvedClosedTransitionsDigest",
                "snapshotChanged",
                "hasChanges",
                "proposedRoadmapDiff",
                "requiredClosedTransitions",
                "planDigest"
            ],
            "Roadmap automation plan has an unsupported shape.");
        if (GetRequiredInteger(root, "schemaVersion") != CurrentSchemaVersion)
        {
            throw new InvalidOperationException("Roadmap automation plan schema version is unsupported.");
        }

        var repository = ValidateRepository(GetRequiredString(root, "repository"));
        var runId = GetRequiredLong(root, "runId");
        ArgumentOutOfRangeException.ThrowIfLessThan(runId, 1);
        var baseSha = ValidateSha(GetRequiredString(root, "baseSha"), "base SHA");
        var branchName = GetRequiredString(root, "branchName");
        if (!string.Equals(branchName, CreateBranchName(runId, baseSha), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan branch name is invalid.");
        }

        var defaultBranchOid = ValidateSha(GetRequiredString(root, "defaultBranchOid"), "default-branch OID");
        var snapshotDigest = ValidateDigest(GetRequiredString(root, "snapshotDigest"), "GitHub snapshot digest");
        var inputDigest = ValidateDigest(GetRequiredString(root, "repositoryInputDigest"), "repository input digest");
        var manifest = ValidateManifestPath(GetRequiredString(root, "reconciliationManifest"));
        var baselineDigest = ValidateDigest(GetRequiredString(root, "baselineClosedTransitionsDigest"), "baseline transition digest");
        var approvedDigest = ValidateDigest(GetRequiredString(root, "approvedClosedTransitionsDigest"), "approved transition digest");
        var snapshotChanged = GetRequiredBoolean(root, "snapshotChanged");
        var hasChanges = GetRequiredBoolean(root, "hasChanges");
        var transitions = ReadTransitions(root);
        var proposed = GetRequiredObject(root, "proposedRoadmapDiff");
        ValidateObjectProperties(
            proposed,
            ["reconciliationManifest", "intakeRoadmapItems", "order", "requiredClosedTransitionCount"],
            "Roadmap automation plan diff metadata has an unsupported shape.");
        var expectedHasChanges = snapshotChanged || transitions.Length > 0;
        if (hasChanges != expectedHasChanges
            || GetRequiredBoolean(proposed, "reconciliationManifest") != expectedHasChanges
            || GetRequiredBoolean(proposed, "intakeRoadmapItems") != expectedHasChanges
            || GetRequiredBoolean(proposed, "order") != expectedHasChanges
            || GetRequiredInteger(proposed, "requiredClosedTransitionCount") != transitions.Length)
        {
            throw new InvalidOperationException("Roadmap automation plan diff metadata is inconsistent.");
        }

        var storedDigest = ValidateDigest(GetRequiredString(root, "planDigest"), "plan digest");
        var plan = new RoadmapAutomationPlan(
            repository,
            runId,
            branchName,
            baseSha,
            defaultBranchOid,
            snapshotDigest,
            inputDigest,
            manifest,
            baselineDigest,
            approvedDigest,
            transitions,
            snapshotChanged,
            storedDigest);
        var computedDigest = plan.ComputePlanDigest();
        if (!string.Equals(storedDigest, computedDigest, StringComparison.Ordinal)
            || !string.Equals(storedDigest, normalizedExpectedDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan digest does not match its content.");
        }

        return plan;
    }

    /// <summary>Writes the plan as indented JSON to <paramref name="path"/> through a rollback-safe atomic write.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is <see langword="null"/>, empty, or white-space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="path"/> has no parent directory.</exception>
    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException("Roadmap automation plan path has no parent directory.");
        Directory.CreateDirectory(directory);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteDocument(writer, includePlanDigest: true);
        }

        var content = Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
        RollbackFileWriteBatch.Apply(
            directory,
            [new AtomicFileWrite(path, content, ExpectedContent: null)]);
    }

    /// <summary>Returns the lowercase SHA-256 hex digest of the transitions serialized as JSON in issue order.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="transitions"/> is <see langword="null"/>.</exception>
    internal static string ComputeTransitionsDigest(IEnumerable<RoadmapClosedItemTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var transition in transitions.OrderBy(transition => transition.Issue))
            {
                writer.WriteStartObject();
                writer.WriteNumber("issue", transition.Issue);
                writer.WriteString("roadmapItem", transition.RoadmapItem);
                writer.WriteString("closeReason", transition.CloseReason);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    /// <summary>Throws when <paramref name="roadmapItem"/> is not a valid roadmap item id.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="roadmapItem"/> is not a valid roadmap item ID.</exception>
    internal static void ValidateRoadmapItemId(string roadmapItem)
    {
        if (string.IsNullOrWhiteSpace(roadmapItem)
            || !roadmapItem.StartsWith("RM-", StringComparison.Ordinal)
            || roadmapItem.Length < 6
            || !roadmapItem[3..].All(char.IsAsciiDigit))
        {
            throw new InvalidOperationException("Roadmap automation plan contains an invalid roadmap item ID.");
        }
    }

    private RoadmapAutomationPlan WithPlanDigest(string planDigest) => new(
        Repository,
        RunId,
        BranchName,
        BaseSha,
        DefaultBranchOid,
        SnapshotDigest,
        RepositoryInputDigest,
        ReconciliationManifest,
        BaselineClosedTransitionsDigest,
        ApprovedClosedTransitionsDigest,
        [.. RequiredClosedTransitions],
        SnapshotChanged,
        planDigest);

    private string ComputePlanDigest()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteDocument(writer, includePlanDigest: false);
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private void WriteDocument(Utf8JsonWriter writer, bool includePlanDigest)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
        writer.WriteString("repository", Repository);
        writer.WriteNumber("runId", RunId);
        writer.WriteString("branchName", BranchName);
        writer.WriteString("baseSha", BaseSha);
        writer.WriteString("defaultBranchOid", DefaultBranchOid);
        writer.WriteString("snapshotDigest", SnapshotDigest);
        writer.WriteString("repositoryInputDigest", RepositoryInputDigest);
        writer.WriteString("reconciliationManifest", ReconciliationManifest);
        writer.WriteString("baselineClosedTransitionsDigest", BaselineClosedTransitionsDigest);
        writer.WriteString("approvedClosedTransitionsDigest", ApprovedClosedTransitionsDigest);
        writer.WriteBoolean("snapshotChanged", SnapshotChanged);
        writer.WriteBoolean("hasChanges", HasChanges);
        writer.WritePropertyName("proposedRoadmapDiff");
        writer.WriteStartObject();
        writer.WriteBoolean("reconciliationManifest", HasChanges);
        writer.WriteBoolean("intakeRoadmapItems", HasChanges);
        writer.WriteBoolean("order", HasChanges);
        writer.WriteNumber("requiredClosedTransitionCount", RequiredClosedTransitions.Count);
        writer.WriteEndObject();
        writer.WritePropertyName("requiredClosedTransitions");
        writer.WriteStartArray();
        foreach (var transition in RequiredClosedTransitions)
        {
            writer.WriteStartObject();
            writer.WriteNumber("issue", transition.Issue);
            writer.WriteString("roadmapItem", transition.RoadmapItem);
            writer.WriteString("closeReason", transition.CloseReason);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        if (includePlanDigest)
        {
            writer.WriteString("planDigest", PlanDigest);
        }

        writer.WriteEndObject();
    }

    private static RoadmapClosedItemTransition[] ReadTransitions(JsonElement root)
    {
        var entries = GetRequiredArray(root, "requiredClosedTransitions");
        List<RoadmapClosedItemTransition> transitions = [];
        foreach (var entry in entries.EnumerateArray())
        {
            ValidateObjectProperties(
                entry,
                ["issue", "roadmapItem", "closeReason"],
                "Roadmap automation plan closure transition has an unsupported shape.");
            transitions.Add(new RoadmapClosedItemTransition(
                GetRequiredInteger(entry, "issue"),
                GetRequiredString(entry, "roadmapItem"),
                GetRequiredString(entry, "closeReason")));
        }

        var result = transitions.ToArray();
        ValidateTransitions(result);
        if (!result.SequenceEqual(result.OrderBy(transition => transition.Issue)))
        {
            throw new InvalidOperationException("Roadmap automation plan closure transitions are not canonically ordered.");
        }

        return result;
    }

    private static RoadmapClosedItemTransition[] MergeTransitions(
        IEnumerable<RoadmapClosedItemTransition> baseline,
        IEnumerable<RoadmapClosedItemTransition> required)
    {
        var merged = baseline.Concat(required).OrderBy(transition => transition.Issue).ToArray();
        if (merged.Select(transition => transition.Issue).Distinct().Count() != merged.Length
            || merged.Select(transition => transition.RoadmapItem).Distinct(StringComparer.Ordinal).Count() != merged.Length)
        {
            throw new InvalidOperationException("Roadmap automation transitions must map unique issues to unique roadmap items.");
        }

        return merged;
    }

    private static void ValidateTransitions(IEnumerable<RoadmapClosedItemTransition> transitions)
    {
        HashSet<int> issues = [];
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        foreach (var transition in transitions)
        {
            ArgumentNullException.ThrowIfNull(transition);
            ValidateRoadmapItemId(transition.RoadmapItem);
            if (!RoadmapCloseReason.IsValid(transition.CloseReason))
            {
                throw new InvalidOperationException("Roadmap automation plan contains an invalid close reason.");
            }

            if (transition.Issue < 1
                || !issues.Add(transition.Issue)
                || !itemIds.Add(transition.RoadmapItem))
            {
                throw new InvalidOperationException("Roadmap automation transitions must map unique issues to unique roadmap items.");
            }
        }
    }

    private static string CreateBranchName(long runId, string baseSha) =>
        $"roadmap-automation/run-{runId}-{baseSha[..12]}";

    private static string ValidateRepository(string repository)
    {
        if (!GitHubRepositoryName.IsValid(repository))
        {
            throw new InvalidOperationException("Roadmap automation plan repository is invalid.");
        }

        return repository;
    }

    private static string ValidateManifestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Any(segment => segment is "" or "." or "..")
            || !path.StartsWith("roadmap/reconciliation/", StringComparison.Ordinal)
            || !path.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Roadmap automation plan reconciliation manifest path is invalid.");
        }

        return path;
    }

    /// <summary>Returns <paramref name="value"/> when it is a 40-character lowercase hex SHA, otherwise throws naming <paramref name="name"/>.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="value"/> is not a 40-character lowercase hex SHA.</exception>
    internal static string ValidateSha(string value, string name)
    {
        if (!IsLowerHex(value, 40))
        {
            throw new InvalidOperationException($"Roadmap automation plan {name} is invalid.");
        }

        return value;
    }

    private static string ValidateDigest(string value, string name)
    {
        if (!IsLowerHex(value, 64))
        {
            throw new InvalidOperationException($"Roadmap automation plan {name} is invalid.");
        }

        return value;
    }

    private static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void ValidateObjectProperties(JsonElement element, string[] expected, string error)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(error);
        }

        var properties = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (properties.Length != expected.Length
            || properties.Distinct(StringComparer.Ordinal).Count() != properties.Length
            || properties.Except(expected, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException(error);
        }
    }

    private static JsonElement GetRequiredObject(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Roadmap automation plan has an unsupported shape.");
        }

        return value;
    }

    private static JsonElement GetRequiredArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Roadmap automation plan has an unsupported shape.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException("Roadmap automation plan has an unsupported shape.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static int GetRequiredInteger(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result))
        {
            throw new InvalidOperationException("Roadmap automation plan has an unsupported shape.");
        }

        return result;
    }

    private static long GetRequiredLong(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetInt64(out var result))
        {
            throw new InvalidOperationException("Roadmap automation plan has an unsupported shape.");
        }

        return result;
    }

    private static bool GetRequiredBoolean(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidOperationException("Roadmap automation plan has an unsupported shape.");
        }

        return value.GetBoolean();
    }
}
