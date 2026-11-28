using System.CommandLine;
using SharedKernel.Processes;
using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

/// <summary>Provides a scripted create-candidate checkout that packs exact fixture archives.</summary>
internal sealed class CandidateSymbolPackFixture : IDisposable
{
    private static readonly string[] RestoredPackageIds = ["pandabear.sharedkernel.probe", "pandabear.sharedkernel.otherprobe"];
    private readonly TemporaryDirectory _directory = new();

    /// <summary>Gets the owned synthetic repository path.</summary>
    internal string Root => _directory.Path;

    /// <summary>Gets the coordinated package output directory.</summary>
    internal string PackageDirectory => Path.Combine(Root, "artifacts", "api-compat", "packages", CandidateSourceLinkTestFeed.PackageVersion);

    /// <summary>Creates the minimal repository consumed by the candidate launcher and configures its scripted pack and audit commands.</summary>
    internal async Task<RootCommand> CreateCommand(string archives, CancellationToken ct)
    {
        var projectDirectory = Path.Combine(Root, "src", "SharedKernel", "SharedKernel.Probe");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "SharedKernel.Probe.csproj"), "<Project />", ct);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Shipped.txt"), string.Empty, ct);
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "PublicAPI.Unshipped.txt"), string.Empty, ct);
        await File.WriteAllTextAsync(Path.Combine(Root, "Directory.Build.props"),
            $"""
            <Project>
              <PropertyGroup>
                <TargetFramework>{TestTargetFramework.Moniker}</TargetFramework>
                <ComputedSemVer>{CandidateSourceLinkTestFeed.PackageVersion}</ComputedSemVer>
              </PropertyGroup>
            </Project>
            """, ct);

        var runner = ScriptedProcessRunner.Answering(run =>
        {
            switch (run.Arguments[0])
            {
                case "status":
                    return new ProcessOutput(0, string.Empty);
                case "rev-parse":
                    return new ProcessOutput(0, CandidateSourceLinkTestFeed.CandidateRevision);
                case "pack":
                    foreach (var archive in Directory.EnumerateFiles(archives, "*.nupkg").Concat(Directory.EnumerateFiles(archives, "*.snupkg")))
                    {
                        File.Copy(archive, Path.Combine(PackageDirectory, Path.GetFileName(archive)));
                    }

                    return new ProcessOutput(0, string.Empty);
                case "restore":
                    var cache = run.Arguments.SkipWhile(static argument => argument != "--packages").ElementAt(1);
                    foreach (var id in RestoredPackageIds)
                    {
                        Directory.CreateDirectory(Path.Combine(cache, id, CandidateSourceLinkTestFeed.PackageVersion));
                    }

                    return new ProcessOutput(0, string.Empty);
                default:
                    throw new InvalidOperationException("Unexpected candidate fixture process.");
            }
        });
        var executables = new ExecutableMap(command => Path.Combine(Root, command));
        return VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, executables);
    }

    /// <inheritdoc />
    public void Dispose() => _directory.Dispose();
}
