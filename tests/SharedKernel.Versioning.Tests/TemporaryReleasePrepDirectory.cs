namespace SharedKernel.Versioning.Tests;

internal sealed class TemporaryReleasePrepDirectory : IDisposable
{
    public TemporaryReleasePrepDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), $"release-prep-{Guid.NewGuid():N}");
        PackageDirectory = Path.Combine(Root, "packages");
        OutputDirectory = Path.Combine(Root, "release-prep");
        Directory.CreateDirectory(PackageDirectory);
        File.WriteAllText(
            Path.Combine(Root, "Directory.Build.props"),
            """
            <Project>
              <PropertyGroup>
                <RepositoryUrl>https://github.com/danigutsch/ViajantesTurismo</RepositoryUrl>
              </PropertyGroup>
            </Project>
            """ + Environment.NewLine);
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
