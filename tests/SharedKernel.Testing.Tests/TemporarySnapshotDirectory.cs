using SharedKernel.Testing.Snapshots;

namespace SharedKernel.Testing.Tests;

internal sealed class TemporarySnapshotDirectory : IDisposable
{
    private readonly string rootDirectory;

    private TemporarySnapshotDirectory(string rootDirectory)
    {
        this.rootDirectory = rootDirectory;
        CanonicalDirectory = Path.Combine(rootDirectory, "canonical");
        GeneratedDirectory = Path.Combine(CanonicalDirectory, ".generated");
        Directory.CreateDirectory(CanonicalDirectory);
        Directory.CreateDirectory(GeneratedDirectory);
    }

    private string CanonicalDirectory { get; }

    private string GeneratedDirectory { get; }

    public static TemporarySnapshotDirectory Create()
    {
        return new TemporarySnapshotDirectory(Path.Combine(Path.GetTempPath(), $"snapshot-{Guid.NewGuid():N}"));
    }

    public void WriteCanonical(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(CanonicalDirectory, fileName), content);
    }

    public void WriteGenerated(string fileName, string content)
    {
        File.WriteAllText(Path.Combine(GeneratedDirectory, fileName), content);
    }

    public JsonSnapshotArtifactSet CreateSet()
    {
        return new JsonSnapshotArtifactSet(
            CanonicalDirectory,
            GeneratedDirectory,
            ".openapi.json",
            "Api_",
            "OpenAPI",
            "Refresh snapshots.");
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
