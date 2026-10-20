using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SharedKernel.RepoConfig.Tool;

internal sealed class RoadmapAutomationResultSeal
{
    private const int CurrentSchemaVersion = 1;
    private const int MaxSealBytes = 64 * 1024;

    private RoadmapAutomationResultSeal(
        string planDigest,
        string artifactDigest,
        string baseSha,
        string snapshotDigest,
        string resultDigest,
        int pathCount,
        bool hasChanges,
        string sealDigest)
    {
        PlanDigest = planDigest;
        ArtifactDigest = artifactDigest;
        BaseSha = baseSha;
        SnapshotDigest = snapshotDigest;
        ResultDigest = resultDigest;
        PathCount = pathCount;
        HasChanges = hasChanges;
        SealDigest = sealDigest;
    }

    public string ArtifactDigest { get; }

    public string BaseSha { get; }

    public bool HasChanges { get; }

    public int PathCount { get; }

    public string PlanDigest { get; }

    public string ResultDigest { get; }

    public string SealDigest { get; }

    public string SnapshotDigest { get; }

    public static RoadmapAutomationResultSeal Create(
        RoadmapAutomationPlan plan,
        string artifactDigest,
        string resultDigest,
        int pathCount,
        bool hasChanges)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var normalizedArtifactDigest = ValidateDigest(artifactDigest);
        var normalizedResultDigest = ValidateDigest(resultDigest);
        if (pathCount < 0 || hasChanges != (pathCount > 0))
        {
            throw new InvalidOperationException("Roadmap automation result seal path metadata is inconsistent.");
        }

        var unsigned = new RoadmapAutomationResultSeal(
            plan.PlanDigest,
            normalizedArtifactDigest,
            plan.BaseSha,
            plan.SnapshotDigest,
            normalizedResultDigest,
            pathCount,
            hasChanges,
            sealDigest: string.Empty);
        return unsigned.WithSealDigest(unsigned.ComputeSealDigest());
    }

    public static RoadmapAutomationResultSeal Load(
        string path,
        string expectedPlanDigest,
        string expectedArtifactDigest,
        string expectedResultDigest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var planDigest = ValidateDigest(expectedPlanDigest);
        var artifactDigest = ValidateDigest(expectedArtifactDigest);
        var resultDigest = ValidateDigest(expectedResultDigest);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is <= 0 or > MaxSealBytes)
        {
            throw CreateInvalidException();
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            var properties = root.ValueKind == JsonValueKind.Object
                ? root.EnumerateObject().Select(property => property.Name).ToArray()
                : [];
            string[] expectedProperties =
            [
                "schemaVersion",
                "planDigest",
                "artifactDigest",
                "baseSha",
                "snapshotDigest",
                "resultDigest",
                "pathCount",
                "hasChanges",
                "sealDigest"
            ];
            if (properties.Length != expectedProperties.Length
                || properties.Distinct(StringComparer.Ordinal).Count() != properties.Length
                || properties.Except(expectedProperties, StringComparer.Ordinal).Any()
                || root.GetProperty("schemaVersion").GetInt32() != CurrentSchemaVersion)
            {
                throw CreateInvalidException();
            }

            var stored = new RoadmapAutomationResultSeal(
                ValidateDigest(root.GetProperty("planDigest").GetString() ?? string.Empty),
                ValidateDigest(root.GetProperty("artifactDigest").GetString() ?? string.Empty),
                ValidateSha(root.GetProperty("baseSha").GetString() ?? string.Empty),
                ValidateDigest(root.GetProperty("snapshotDigest").GetString() ?? string.Empty),
                ValidateDigest(root.GetProperty("resultDigest").GetString() ?? string.Empty),
                root.GetProperty("pathCount").GetInt32(),
                root.GetProperty("hasChanges").GetBoolean(),
                ValidateDigest(root.GetProperty("sealDigest").GetString() ?? string.Empty));
            if (stored.PathCount < 0
                || stored.HasChanges != (stored.PathCount > 0)
                || !string.Equals(stored.PlanDigest, planDigest, StringComparison.Ordinal)
                || !string.Equals(stored.ArtifactDigest, artifactDigest, StringComparison.Ordinal)
                || !string.Equals(stored.ResultDigest, resultDigest, StringComparison.Ordinal)
                || !string.Equals(stored.SealDigest, stored.ComputeSealDigest(), StringComparison.Ordinal))
            {
                throw CreateInvalidException();
            }

            return stored;
        }
        catch (Exception exception) when (exception is JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or FormatException)
        {
            throw CreateInvalidException();
        }
    }

    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException("Roadmap automation result seal path has no parent directory.");
        Directory.CreateDirectory(directory);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteDocument(writer, includeSealDigest: true);
        }

        var content = Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine;
        RollbackFileWriteBatch.Apply(
            directory,
            [new AtomicFileWrite(path, content, ExpectedContent: null)]);
    }

    private RoadmapAutomationResultSeal WithSealDigest(string sealDigest) => new(
        PlanDigest,
        ArtifactDigest,
        BaseSha,
        SnapshotDigest,
        ResultDigest,
        PathCount,
        HasChanges,
        sealDigest);

    private string ComputeSealDigest()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteDocument(writer, includeSealDigest: false);
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private void WriteDocument(Utf8JsonWriter writer, bool includeSealDigest)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
        writer.WriteString("planDigest", PlanDigest);
        writer.WriteString("artifactDigest", ArtifactDigest);
        writer.WriteString("baseSha", BaseSha);
        writer.WriteString("snapshotDigest", SnapshotDigest);
        writer.WriteString("resultDigest", ResultDigest);
        writer.WriteNumber("pathCount", PathCount);
        writer.WriteBoolean("hasChanges", HasChanges);
        if (includeSealDigest)
        {
            writer.WriteString("sealDigest", SealDigest);
        }

        writer.WriteEndObject();
    }

    private static string ValidateDigest(string value)
    {
        if (value.Length != 64 || !value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw CreateInvalidException();
        }

        return value;
    }

    private static string ValidateSha(string value)
    {
        if (value.Length != 40 || !value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw CreateInvalidException();
        }

        return value;
    }

    private static InvalidOperationException CreateInvalidException() =>
        new("Roadmap automation result seal is invalid or was tampered with.");
}
