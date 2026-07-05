namespace SharedKernel.Versioning.Tests;

internal sealed class TemporaryReleasePrepDirectory : IDisposable
{
    public TemporaryReleasePrepDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), $"release-prep-{Guid.NewGuid():N}");
        PackageDirectory = Path.Combine(Root, "packages");
        OutputDirectory = Path.Combine(Root, "release-prep");
        Directory.CreateDirectory(PackageDirectory);
    }

    public string Root { get; }

    public string PackageDirectory { get; }

    public string OutputDirectory { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
