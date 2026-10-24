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

    /// <summary>Writes the version 1.0.0 probe package and a manifest with its hash and the hashes of <paramref name="packages" />.</summary>
    public string WriteCandidate(params string[] packages)
    {
        var hashes = new System.Text.Json.Nodes.JsonObject();
        foreach (var package in packages.Prepend(NuGetPackageBuilder.WritePackage(PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0")))
        {
            hashes[Path.GetFileName(package)] = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(package)));
        }

        var path = Path.Combine(PackageDirectory, "candidate.json");
        CandidateManifest.Write(path, new System.Text.Json.Nodes.JsonObject
        {
            ["packageVersion"] = "1.0.0",
            ["packageSha256"] = hashes
        });
        return path;
    }

    /// <summary>
    /// Packs a version 1.0.0 framework-dependent tool into <see cref="PackageDirectory" /> whose command prints
    /// <paramref name="output" /> followed by its arguments.
    /// </summary>
    public async Task<string> PackTool(string packageId, string command, string output, CancellationToken ct)
    {
        var projectDirectory = Path.Combine(Root, "tool-projects", packageId);
        Directory.CreateDirectory(projectDirectory);
        File.Copy(Path.Combine(RepositoryRoot.Find(), "global.json"), Path.Combine(projectDirectory, "global.json"));
        foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props" })
        {
            await File.WriteAllTextAsync(Path.Combine(projectDirectory, name), "<Project />", ct);
        }

        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "ProbeTool.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <PackAsTool>true</PackAsTool>
                <ToolCommandName>{command}</ToolCommandName>
                <PackageId>{packageId}</PackageId>
                <Version>1.0.0</Version>
              </PropertyGroup>
            </Project>
            """,
            ct);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "Program.cs"), $"System.Console.WriteLine(\"{output} \" + string.Join(' ', args));", ct);
        await Tool.CommandRunner.Run("dotnet", ["pack", projectDirectory, "-c", "Release", "-o", PackageDirectory], projectDirectory, ct);
        return Path.Combine(PackageDirectory, packageId + ".1.0.0.nupkg");
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
