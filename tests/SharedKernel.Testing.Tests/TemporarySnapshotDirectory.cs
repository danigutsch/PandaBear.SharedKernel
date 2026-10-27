using PandaBear.Testing.Snapshots;

namespace PandaBear.Testing.Tests;

/// <summary>Temporary directory pre-laid with a canonical snapshot folder and a nested <c>.generated</c> folder, deleted on disposal.</summary>
internal sealed class TemporarySnapshotDirectory : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private TemporarySnapshotDirectory()
    {
        CanonicalDirectory = Path.Combine(_directory.Path, "canonical");
        GeneratedDirectory = Path.Combine(CanonicalDirectory, ".generated");
        Directory.CreateDirectory(CanonicalDirectory);
        Directory.CreateDirectory(GeneratedDirectory);
    }

    private string CanonicalDirectory
    {
        get;
    }

    private string GeneratedDirectory
    {
        get;
    }

    /// <summary>Creates a new temporary directory with empty canonical and generated subfolders.</summary>
    /// <returns>The new directory wrapper; the caller disposes it.</returns>
    public static TemporarySnapshotDirectory Create()
    {
        return new TemporarySnapshotDirectory();
    }

    /// <summary>Writes a file into the canonical snapshot folder.</summary>
    /// <param name="fileName">The file name relative to the canonical folder.</param>
    /// <param name="content">The text to write.</param>
    public void WriteCanonical(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(CanonicalDirectory, fileName), content);
    }

    /// <summary>Writes a file into the <c>.generated</c> folder that holds freshly produced artifacts.</summary>
    /// <param name="fileName">The file name relative to the generated folder.</param>
    /// <param name="content">The text to write.</param>
    public void WriteGenerated(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(GeneratedDirectory, fileName), content);
    }

    /// <summary>Builds an OpenAPI JSON artifact set over the two folders, using the <c>.openapi.json</c> suffix and the <c>Api_</c> generated-file prefix.</summary>
    /// <param name="generatedFileNameOverrides">Generated file names keyed by snapshot name, or <see langword="null" /> to use the prefix convention.</param>
    /// <returns>The configured artifact set.</returns>
    public JsonSnapshotArtifactSet CreateSet(IReadOnlyDictionary<string, string>? generatedFileNameOverrides = null)
    {
        return new JsonSnapshotArtifactSet(
            CanonicalDirectory,
            GeneratedDirectory,
            ".openapi.json",
            "Api_",
            "OpenAPI",
            "Refresh snapshots.",
            generatedFileNameOverrides);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
