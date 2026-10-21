namespace PandaBear.Versioning.Tests;

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
                <RepositoryUrl>https://github.com/danigutsch/PandaBear.SharedKernel</RepositoryUrl>
              </PropertyGroup>
            </Project>
            """ + Environment.NewLine);
    }

    public string Root
    {
        get;
    }

    public string PackageDirectory
    {
        get;
    }

    public string OutputDirectory
    {
        get;
    }

    public string WriteCandidate()
    {
        var package = NuGetPackageBuilder.WritePackage(PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(package)));
        var path = Path.Combine(PackageDirectory, "candidate.json");
        Tool.CandidateManifest.Write(path, new System.Text.Json.Nodes.JsonObject
        {
            ["packageVersion"] = "1.0.0",
            ["packageSha256"] = new System.Text.Json.Nodes.JsonObject { [Path.GetFileName(package)] = hash }
        });
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
