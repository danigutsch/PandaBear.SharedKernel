using PandaBear.Tooling;

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
        CandidateManifest.Write(path, new System.Text.Json.Nodes.JsonObject
        {
            ["packageVersion"] = "1.0.0",
            ["packageSha256"] = new System.Text.Json.Nodes.JsonObject { [Path.GetFileName(package)] = hash }
        });
        return path;
    }

    public async Task WriteConsumptionSamples(string sampleOutput, CancellationToken ct)
    {
        File.Copy(Path.Combine(RepositoryRoot.Find(), "global.json"), Path.Combine(Root, "global.json"));
        await File.WriteAllTextAsync(Path.Combine(Root, "Directory.Packages.props"), "<Project />", ct);
        foreach (var sample in new[] { "samples/Results/BasicResults.Sample", "samples/Mediator/Mediator.Sample", "samples/HttpRequests/HttpRequests.Sample" })
        {
            var sampleDirectory = Path.Combine(Root, sample);
            Directory.CreateDirectory(sampleDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(sampleDirectory, Path.GetFileName(sample) + ".csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>",
                ct);
            await File.WriteAllTextAsync(Path.Combine(sampleDirectory, "Program.cs"), $"System.Console.WriteLine(\"{sampleOutput}\");", ct);
        }
    }

    public async Task WriteUnpackableSharedKernelProject(CancellationToken ct)
    {
        var projectDirectory = Path.Combine(Root, "src", "SharedKernel", "PandaBear.Sample");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SharedKernel.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>not-a-tfm</TargetFramework><PackageId>PandaBear.SharedKernel.Sample</PackageId></PropertyGroup></Project>",
            ct);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), "#nullable enable", ct);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), "#nullable enable", ct);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
