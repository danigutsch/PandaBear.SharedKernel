using PandaBear.Testing.Snapshots;

namespace PandaBear.Testing.Tests;

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

    public static TemporarySnapshotDirectory Create()
    {
        return new TemporarySnapshotDirectory();
    }

    public void WriteCanonical(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(CanonicalDirectory, fileName), content);
    }

    public void WriteGenerated(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(GeneratedDirectory, fileName), content);
    }

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

    public void Dispose() => _directory.Dispose();
}
