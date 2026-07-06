using System.Text.Json.Nodes;

namespace SharedKernel.Testing.Snapshots;

/// <summary>
/// Compares generated JSON artifacts with committed canonical snapshots.
/// </summary>
public sealed class JsonSnapshotArtifactSet
{
    private readonly string canonicalDirectory;
    private readonly string generatedDirectory;
    private readonly string canonicalArtifactSuffix;
    private readonly string generatedArtifactPrefix;
    private readonly string artifactDisplayName;
    private readonly string refreshHint;

    /// <summary>
    /// Initializes a new JSON snapshot artifact set.
    /// </summary>
    /// <param name="canonicalDirectory">The directory containing committed canonical snapshots.</param>
    /// <param name="generatedDirectory">The directory containing generated artifacts.</param>
    /// <param name="canonicalArtifactSuffix">The canonical artifact suffix.</param>
    /// <param name="generatedArtifactPrefix">The generated artifact prefix.</param>
    /// <param name="artifactDisplayName">A human-readable artifact name for failure messages.</param>
    /// <param name="refreshHint">The refresh hint for failure messages.</param>
    public JsonSnapshotArtifactSet(
        string canonicalDirectory,
        string generatedDirectory,
        string canonicalArtifactSuffix,
        string generatedArtifactPrefix,
        string artifactDisplayName,
        string refreshHint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalArtifactSuffix);
        ArgumentNullException.ThrowIfNull(generatedArtifactPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshHint);

        this.canonicalDirectory = canonicalDirectory;
        this.generatedDirectory = generatedDirectory;
        this.canonicalArtifactSuffix = canonicalArtifactSuffix;
        this.generatedArtifactPrefix = generatedArtifactPrefix;
        this.artifactDisplayName = artifactDisplayName;
        this.refreshHint = refreshHint;
    }

    /// <summary>
    /// Asserts that every canonical snapshot has a matching generated artifact and vice versa.
    /// </summary>
    public void AssertCanonicalArtifactsMatchGeneratedArtifacts()
    {
        var failures = GetArtifactDrift();

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"{artifactDisplayName} snapshot drift detected. {refreshHint}"
                + Environment.NewLine
                + string.Join(Environment.NewLine, failures));
        }
    }

    /// <summary>
    /// Asserts that one generated artifact matches its committed canonical snapshot.
    /// </summary>
    /// <param name="snapshotName">The logical snapshot name.</param>
    public void AssertGeneratedArtifactMatchesCanonicalSnapshot(string snapshotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var canonicalSnapshot = GetCanonicalSnapshot(snapshotName);
        var generatedArtifact = GetGeneratedArtifact(snapshotName);

        if (!JsonNode.DeepEquals(canonicalSnapshot, generatedArtifact))
        {
            var generatedFileName = GetGeneratedFileName(snapshotName);
            var canonicalFileName = GetCanonicalFileName(snapshotName);
            throw new InvalidOperationException(
                $"Generated {artifactDisplayName} artifact '{generatedFileName}' does not match canonical snapshot '{canonicalFileName}'. {refreshHint}");
        }
    }

    /// <summary>
    /// Gets all canonical-versus-generated artifact drift messages.
    /// </summary>
    /// <returns>The detected drift messages.</returns>
    public IReadOnlyList<string> GetArtifactDrift()
    {
        EnsureDirectoryExists(canonicalDirectory, $"Canonical {artifactDisplayName} snapshots were not found at '{canonicalDirectory}'.");
        EnsureDirectoryExists(generatedDirectory, $"Generated {artifactDisplayName} artifacts were not found at '{generatedDirectory}'. {refreshHint}");

        var canonicalFiles = Directory.GetFiles(canonicalDirectory, $"*{canonicalArtifactSuffix}", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        var generatedFiles = Directory.GetFiles(generatedDirectory, $"{generatedArtifactPrefix}*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        var failures = new List<string>();
        var expectedGeneratedFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var canonicalFile in canonicalFiles)
        {
            var snapshotName = GetSnapshotName(canonicalFile);
            var generatedFileName = GetGeneratedFileName(snapshotName);
            var generatedFilePath = Path.Combine(generatedDirectory, generatedFileName);
            expectedGeneratedFiles.Add(generatedFileName);

            if (!File.Exists(generatedFilePath))
            {
                failures.Add($"Missing generated artifact '{generatedFileName}' for canonical snapshot '{Path.GetFileName(canonicalFile)}'. {refreshHint}");
                continue;
            }

            if (!JsonNode.DeepEquals(ParseJson(canonicalFile), ParseJson(generatedFilePath)))
            {
                failures.Add($"Canonical snapshot '{Path.GetFileName(canonicalFile)}' drifted from generated artifact '{generatedFileName}'. {refreshHint}");
            }
        }

        foreach (var generatedFile in generatedFiles)
        {
            var generatedFileName = Path.GetFileName(generatedFile);
            if (!expectedGeneratedFiles.Contains(generatedFileName))
            {
                failures.Add($"Generated artifact '{generatedFileName}' has no canonical snapshot counterpart. {refreshHint}");
            }
        }

        return failures;
    }

    /// <summary>
    /// Reads one canonical JSON snapshot.
    /// </summary>
    /// <param name="snapshotName">The logical snapshot name.</param>
    /// <returns>The parsed JSON snapshot.</returns>
    public JsonNode GetCanonicalSnapshot(string snapshotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var canonicalFileName = GetCanonicalFileName(snapshotName);
        var canonicalFilePath = Path.Combine(canonicalDirectory, canonicalFileName);
        EnsureFileExists(canonicalFilePath, $"Canonical {artifactDisplayName} snapshot '{canonicalFileName}' was not found at '{canonicalDirectory}'.");

        return ParseJson(canonicalFilePath);
    }

    /// <summary>
    /// Reads one generated JSON artifact.
    /// </summary>
    /// <param name="snapshotName">The logical snapshot name.</param>
    /// <returns>The parsed generated JSON artifact.</returns>
    public JsonNode GetGeneratedArtifact(string snapshotName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotName);

        var generatedFileName = GetGeneratedFileName(snapshotName);
        var generatedFilePath = Path.Combine(generatedDirectory, generatedFileName);
        EnsureFileExists(generatedFilePath, $"Generated {artifactDisplayName} artifact '{generatedFileName}' was not found at '{generatedDirectory}'. {refreshHint}");

        return ParseJson(generatedFilePath);
    }

    /// <summary>
    /// Checks whether two JSON artifacts are structurally equal.
    /// </summary>
    /// <param name="expected">The expected JSON artifact.</param>
    /// <param name="actual">The actual JSON artifact.</param>
    /// <returns><see langword="true" /> when both JSON artifacts are structurally equal.</returns>
    public static bool Equals(JsonNode? expected, JsonNode? actual) => JsonNode.DeepEquals(expected, actual);

    private static JsonNode ParseJson(string documentPath)
    {
        var documentText = File.ReadAllText(documentPath);
        return JsonNode.Parse(documentText)
            ?? throw new InvalidOperationException($"JSON artifact '{documentPath}' could not be parsed.");
    }

    private static void EnsureDirectoryExists(string directoryPath, string message)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureFileExists(string filePath, string message)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException(message);
        }
    }

    private string GetSnapshotName(string canonicalPath)
    {
        var fileName = Path.GetFileName(canonicalPath);
        if (!fileName.EndsWith(canonicalArtifactSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Canonical snapshot '{fileName}' does not use the expected suffix '{canonicalArtifactSuffix}'.");
        }

        return fileName[..^canonicalArtifactSuffix.Length];
    }

    private string GetGeneratedFileName(string snapshotName) => $"{generatedArtifactPrefix}{snapshotName}.json";

    private string GetCanonicalFileName(string snapshotName) => $"{snapshotName}{canonicalArtifactSuffix}";
}
