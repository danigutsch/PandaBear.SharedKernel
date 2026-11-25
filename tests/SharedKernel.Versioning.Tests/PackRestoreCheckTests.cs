using SharedKernel.Processes;

namespace SharedKernel.Versioning.Tests;

/// <summary>
/// Tests the restore check of pack, which restores the packed packages and installs the packed tools from the new folder.
/// </summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class PackRestoreCheckTests
{
    private static readonly ExecutableMap UsrBin = new(static command => "/usr/bin/" + command);

    [Fact]
    public static async Task Restore_check_audits_the_packages_that_a_tool_carries_before_it_installs_the_tool()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var project = Path.Combine(temporary.Root, "src", "SharedKernel", "SharedKernel.Probe", "SharedKernel.Probe.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        await File.WriteAllTextAsync(project, "<Project />", TestContext.Current.CancellationToken);
        var options = new PackOptions("1.0.0", Path.Combine(temporary.Root, "packages-out"), temporary.Root);
        var packageDirectory = PackPlanner.ResolvePackageDirectory(options);
        var runner = ScriptedProcessRunner.Answering(run =>
        {
            var arguments = run.Arguments;
            if (arguments[0] == "pack")
            {
                NuGetPackageBuilder.WritePackage(packageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
                NuGetPackageBuilder.WriteToolPackage(
                    packageDirectory,
                    "PandaBear.SharedKernel.Probe.Tool",
                    "1.0.0",
                    (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")),
                    (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing("System.CommandLine/3.0.0")));
            }
            else if (arguments[0] == "restore" && arguments[1].EndsWith("SharedKernel.LocalFeedRestore.csproj", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.Combine(arguments.SkipWhile(static argument => argument != "--packages").ElementAt(1), "pandabear.sharedkernel.probe", "1.0.0"));
            }

            return new ProcessOutput(0, string.Empty);
        });

        // Act
        await ReleasePack.Run(new ExternalCommands(runner, UsrBin), options, verifyRestore: true, baselineVersion: null, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        var started = runner.Runs.Select(static run => run.Arguments.ToArray()).ToArray();
        started.Select(static arguments => arguments[0]).ShouldBe(
            ["pack", "restore", "restore", "tool"],
            "The packages restore, then the packages that the tool carries are audited before the tool installs.");
        started[2][1].ShouldBe(Path.Combine(packageDirectory, ".restore-check", "tool-audit", "0", "ToolAudit.csproj"));
        var configuration = await File.ReadAllTextAsync(Path.Combine(packageDirectory, ".restore-check", "tool-audit", "NuGet.config"), TestContext.Current.CancellationToken);
        configuration.ShouldContain("<auditSources>", StringComparison.Ordinal, "The tool audit has nuget.org as its audit source.");
    }
}
