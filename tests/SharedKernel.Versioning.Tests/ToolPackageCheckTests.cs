using System.Xml;
using System.Xml.Linq;

namespace SharedKernel.Versioning.Tests;

/// <summary>
/// Tests that consume-candidate and ToolPackageCheck install tool packages from the candidate directory and run each command with --help.
/// </summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class ToolPackageCheckTests
{
    private static readonly Uri NuGetOrg = new("https://api.nuget.org/v3/index.json");

    [Fact]
    public static async Task Consume_candidate_installs_each_tool_package_and_runs_its_command_with_help()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var toolOutput = "Probe tool " + Guid.NewGuid().ToString("N");
        var tool = await temporary.PackTool("PandaBear.SharedKernel.Probe.Tool", "sharedkernel-probe", toolOutput, TestContext.Current.CancellationToken);
        var manifest = temporary.WriteCandidate(tool);
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);
        using var evidence = new EvidenceWorkspaces(result);

        // Assert
        var succeeded = result.ExitCode == 0;
        succeeded.ShouldBeTrue($"consume-candidate exited with {result.ExitCode}. Standard error:{Environment.NewLine}{result.Error}");
        result.Error.ShouldContain(toolOutput + " --help", StringComparison.Ordinal);
        var recorded = CandidateManifest.Load(manifest).Evidence[platform].ShouldNotBeNull().GetValue<string>();
        recorded.ShouldBe("passed", "Every tool command answered --help, and the samples ran.");
    }

    [Fact]
    public static async Task Consume_candidate_fails_naming_a_tool_package_that_does_not_install()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Broken.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-broken")),
            (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing()));
        var manifest = temporary.WriteCandidate(tool);
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);
        var platform = OperatingSystem.IsWindows() ? "windowsPackageConsumption" : "linuxPackageConsumption";

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);
        using var evidence = new EvidenceWorkspaces(result);

        // Assert
        result.ExitCode.ShouldBe(2, "The package lacks the entry point that its tool settings name, so dotnet tool install fails.");
        result.Error.ShouldContain("Tool package PandaBear.SharedKernel.Broken.Tool did not install", StringComparison.Ordinal);
        var recorded = CandidateManifest.Load(manifest).Evidence[platform].ShouldNotBeNull().GetValue<string>();
        recorded.ShouldBe("failed");
    }

    [Fact]
    public static async Task Consume_candidate_rejects_a_tool_package_of_another_version_before_installing_tools()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "2.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")));
        var manifest = temporary.WriteCandidate(tool);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2, "Tools install from the candidate directory, which holds packages of the candidate version only.");
        result.Error.ShouldContain("expected version 1.0.0, found 2.0.0", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Consume_candidate_rejects_packages_that_the_manifest_does_not_list_before_installing_tools()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        await temporary.WriteConsumptionSamples("Consumed sample", TestContext.Current.CancellationToken);
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Unlisted.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-unlisted")));
        var library = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Unlisted", "1.0.0");
        var original = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2, "No hash in the manifest covers the unlisted packages.");
        result.Error.ShouldBe(
            $"Error: Candidate directory {temporary.PackageDirectory} holds packages that the manifest does not list: "
            + $"{Path.GetFileName(library)}, {Path.GetFileName(tool)}.\n",
            "The check names every unlisted package and fails before any dotnet or tool process writes output.");
        var retained = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        retained.ShouldBe(original, "A failure before consumption leaves the recorded evidence unchanged.");
    }

    [Fact(
        SkipType = typeof(FileSystemProbes),
        SkipUnless = nameof(FileSystemProbes.IsCaseSensitive),
        Skip = "This file system opens one file for package file names that differ only in case.")]
    public static async Task Consume_candidate_rejects_a_package_whose_file_name_differs_from_a_listed_one_only_in_case()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var manifest = temporary.WriteCandidate();
        var variant = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "pandabear.sharedkernel.probe", "1.0.0");
        var original = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            ["consume-candidate", "--candidate", manifest, "--repo-root", temporary.Root],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2, "NuGet can restore the unlisted variant instead of the listed PandaBear.SharedKernel.Probe package.");
        result.Error.ShouldBe(
            $"Error: Candidate directory {temporary.PackageDirectory} holds packages that the manifest does not list: "
            + $"{Path.GetFileName(variant)}.\n",
            "File names match manifest entries by ordinal comparison, so case counts.");
        var retained = await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken);
        retained.ShouldBe(original, "A failure before consumption leaves the recorded evidence unchanged.");
    }

    [Fact]
    public static async Task Tool_check_installs_tools_from_the_candidate_directory_alone_and_runs_each_command_from_an_empty_directory()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var library = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe", "sharedkernel-probe-admin")),
            (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing()));
        var runner = new RecordingProcessRunner();
        using var error = new InvariantStringWriter();
        var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        // Act
        await ToolPackageCheck.Run(temporary.Root, temporary.PackageDirectory, "1.0.0", [library, tool], NuGetOrg, runner.Commands, error, TestContext.Current.CancellationToken);

        // Assert
        runner.Processes.Count.ShouldBe(3, "The library package is not a tool; the tool carries no package to audit, installs once, and declares two commands.");
        var (installFileName, installArguments, scratch, configuration, installEntries) = runner.Processes[0];
        var toolPath = Path.Combine(scratch.ShouldNotBeNull(), "tools");
        installFileName.ShouldBe("dotnet");
        installArguments.ShouldBe(
            ["tool", "install", "--tool-path", toolPath, "PandaBear.SharedKernel.Probe.Tool", "--version", "1.0.0", "--configfile", Path.Combine(scratch, "NuGet.config")],
            "The tool installs into the scratch tool path from the generated configuration.");
        installEntries.ShouldContain(Path.Combine(scratch, "global.json"));
        var sources = XDocument.Parse(configuration.ShouldNotBeNull()).Root.ShouldNotBeNull().Elements("packageSources").ShouldHaveSingleItem().Elements().ToArray();
        sources.Select(static source => source.Name.LocalName).ShouldBe(["clear", "add"], "The configuration clears every inherited source before adding the candidate directory.");
        sources[1].Attribute("value").ShouldNotBeNull().Value.ShouldBe(Path.GetFullPath(temporary.PackageDirectory));
        runner.Processes.Skip(1).Select(static process => (process.FileName, string.Join(' ', process.Arguments))).ShouldBe(
            [
                (Path.Combine(toolPath, "sharedkernel-probe" + executableSuffix), "--help"),
                (Path.Combine(toolPath, "sharedkernel-probe-admin" + executableSuffix), "--help"),
            ],
            "Each declared command runs from its installed shim.");
        runner.Processes.Skip(1).ShouldAllSatisfy(process =>
        {
            Path.GetDirectoryName(process.WorkingDirectory).ShouldBe(scratch);
            process.WorkingDirectoryEntries.ShouldBeEmpty();
        });
        error.ToString().ShouldContain(Path.Combine(toolPath, "sharedkernel-probe-admin" + executableSuffix) + " ran", StringComparison.Ordinal);
        Directory.Exists(scratch).ShouldBeFalse("The scratch directory is deleted after the check.");
    }

    [Fact]
    public static async Task Tool_check_names_a_package_that_does_not_install_and_deletes_its_scratch_directory()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing()));
        var runner = new RecordingProcessRunner(failingFileName: "dotnet");
        Func<Task> check = () => ToolPackageCheck.Run(
            temporary.Root, temporary.PackageDirectory, "1.0.0", [tool], NuGetOrg, runner.Commands, TextWriter.Null, TestContext.Current.CancellationToken);

        // Act
        var exception = await check.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith("Tool package PandaBear.SharedKernel.Probe.Tool did not install: Command failed (3): dotnet tool install", StringComparison.Ordinal);
        var scratch = runner.Processes.ShouldHaveSingleItem().WorkingDirectory.ShouldNotBeNull();
        Directory.Exists(scratch).ShouldBeFalse("The scratch directory is deleted when the check fails.");
    }

    [Fact]
    public static async Task Tool_check_names_the_package_and_command_whose_help_fails_and_deletes_its_scratch_directory()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing()));
        var runner = new RecordingProcessRunner(failingFileName: "sharedkernel-probe");
        Func<Task> check = () => ToolPackageCheck.Run(
            temporary.Root, temporary.PackageDirectory, "1.0.0", [tool], NuGetOrg, runner.Commands, TextWriter.Null, TestContext.Current.CancellationToken);

        // Act
        var exception = await check.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(
            "Tool command sharedkernel-probe of package PandaBear.SharedKernel.Probe.Tool failed with --help: Command failed (3): ",
            StringComparison.Ordinal);
        runner.Processes.Count.ShouldBe(2, "The command runs after its package installs.");
        var scratch = runner.Processes[0].WorkingDirectory.ShouldNotBeNull();
        Directory.Exists(scratch).ShouldBeFalse("The scratch directory is deleted when the check fails.");
    }

    [Fact]
    public static async Task Tool_check_audits_the_packages_that_a_tool_carries_before_it_installs_the_tool()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing("System.CommandLine/3.0.0")));
        var auditSource = new Uri("https://audit.example.test/v3/index.json");
        var runner = new RecordingProcessRunner();

        // Act
        await ToolPackageCheck.Run(temporary.Root, temporary.PackageDirectory, "1.0.0", [tool], auditSource, runner.Commands, TextWriter.Null, TestContext.Current.CancellationToken);

        // Assert
        runner.Processes.Select(static process => process.Arguments[0]).ShouldBe(
            ["restore", "tool", "--help"],
            "dotnet tool install runs no NuGet audit, so a restore audits the packages that the tool carries before the tool installs or runs.");
        var (_, restoreArguments, projectDirectory, configuration, _) = runner.Processes[0];
        var auditDirectory = Path.GetDirectoryName(projectDirectory.ShouldNotBeNull()).ShouldNotBeNull();
        restoreArguments.ShouldBe(
            ["restore", Path.Combine(projectDirectory, "ToolAudit.csproj"), "--configfile", Path.Combine(auditDirectory, "NuGet.config"), "--packages", Path.Combine(auditDirectory, "packages"), "--no-http-cache"],
            "The audit restores into a package folder of its own and fetches the vulnerability data again.");
        var root = XDocument.Parse(configuration.ShouldNotBeNull()).Root.ShouldNotBeNull();
        var auditSources = root.Elements("auditSources").ShouldHaveSingleItem().Elements().ToArray();
        auditSources.Select(static source => source.Name.LocalName).ShouldBe(["clear", "add"], "The audit source replaces the package sources, which include the candidate folder that has no vulnerability data.");
        auditSources[1].Attribute("value").ShouldNotBeNull().Value.ShouldBe(auditSource.AbsoluteUri);
        runner.Processes[1].Configuration.ShouldNotBeNull().ShouldNotContain("auditSources", StringComparison.Ordinal, "The install configuration keeps the candidate folder as its only source.");
    }

    [Fact]
    public static async Task Tool_audit_fails_naming_the_tool_and_the_known_vulnerability_of_a_package_it_carries()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPathFor(TestTargetFramework.Moniker), DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPathFor(TestTargetFramework.Moniker), ToolDependencyManifest.Listing("Newtonsoft.Json/12.0.1")));
        Func<Task> audit = () => ToolPackageCheck.Audit(
            Path.Combine(temporary.Root, "audit"), temporary.PackageDirectory, "1.0.0", [tool], NuGetOrg, SystemCommands.Instance, TextWriter.Null, TestContext.Current.CancellationToken);

        // Act
        var exception = await audit.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(
            $"Tool package PandaBear.SharedKernel.Probe.Tool failed the NuGet audit of the packages it carries for {TestTargetFramework.Moniker}: ",
            StringComparison.Ordinal);
        exception.Message.ShouldContain("NU1903", StringComparison.Ordinal, "Newtonsoft.Json 12.0.1 has the high severity advisory GHSA-5crp-9r3c-p9vr.");
        exception.Message.ShouldContain("Newtonsoft.Json", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Tool_audit_fails_when_the_audit_source_does_not_answer()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var library = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPathFor(TestTargetFramework.Moniker), DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPathFor(TestTargetFramework.Moniker), ToolDependencyManifest.Listing("PandaBear.SharedKernel.Probe/1.0.0")));
        var closed = new Uri("https://127.0.0.1:1/v3/index.json");
        Func<Task> audit = () => ToolPackageCheck.Audit(
            Path.Combine(temporary.Root, "audit"), temporary.PackageDirectory, "1.0.0", [library, tool], closed, SystemCommands.Instance, TextWriter.Null, TestContext.Current.CancellationToken);

        // Act
        var exception = await audit.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(
            $"Tool package PandaBear.SharedKernel.Probe.Tool failed the NuGet audit of the packages it carries for {TestTargetFramework.Moniker}: ",
            StringComparison.Ordinal);
        exception.Message.ShouldContain("NU1900", StringComparison.Ordinal, "The package restores from the candidate folder, but no vulnerability data arrives, so nothing was checked.");
    }

    [Fact]
    public static async Task Tool_audit_passes_when_the_audit_source_answers_and_reports_no_vulnerability()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        File.Copy(Path.Combine(RepositoryRoot.Locate("SharedKernel.slnx"), "global.json"), Path.Combine(temporary.Root, "global.json"));
        var library = NuGetPackageBuilder.WritePackage(temporary.PackageDirectory, "PandaBear.SharedKernel.Probe", "1.0.0");
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPathFor(TestTargetFramework.Moniker), DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPathFor(TestTargetFramework.Moniker), ToolDependencyManifest.Listing("PandaBear.SharedKernel.Probe/1.0.0")));
        using var error = new InvariantStringWriter();

        // Act
        await ToolPackageCheck.Audit(
            Path.Combine(temporary.Root, "audit"), temporary.PackageDirectory, "1.0.0", [library, tool], NuGetOrg, SystemCommands.Instance, error, TestContext.Current.CancellationToken);

        // Assert
        error.ToString().ShouldNotContain("NU19", StringComparison.Ordinal, "nuget.org answered and lists no advisory for the package.");
    }

    [Fact]
    public static void Tool_packages_are_read_from_the_dependency_manifests_of_every_framework_dependent_folder()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPath, ToolDependencyManifest.Listing("System.CommandLine/3.0.0", "Microsoft.CodeAnalysis.Common/5.12.0")),
            ("tools/net11.0/any/DotnetToolSettings.xml", DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPathFor("net11.0"), ToolDependencyManifest.Listing()),
            ("tools/net12.0/linux-x64/Probe.Tool.deps.json", ToolDependencyManifest.Listing("Native.Only/1.0.0")));

        // Act
        var frameworks = ToolPackageCheck.ReadCarriedPackages(tool);

        // Assert
        frameworks.Select(static framework => framework.Framework).ShouldBe(["net10.0", "net11.0"], "Only framework-dependent tool folders install with a tool path, as their commands do.");
        frameworks[0].Packages.ShouldBe(
            [("Microsoft.CodeAnalysis.Common", "5.12.0"), ("System.CommandLine", "3.0.0")],
            "Only package libraries are audited; the tool's own project is not a package.");
        frameworks[1].Packages.ShouldBeEmpty();
    }

    [Fact]
    public static void Tool_package_without_a_dependency_manifest_is_rejected()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")));
        Action read = () => ToolPackageCheck.ReadCarriedPackages(tool);

        // Act
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldBe(tool + ": expected a dependency manifest tools/net10.0/any/*.deps.json, which lists the packages to audit");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"libraries":[]}""")]
    [InlineData("""{"libraries":{"System.CommandLine":{"type":"package"}}}""")]
    [InlineData("""{"libraries":{"/3.0.0":{"type":"package"}}}""")]
    [InlineData("""{"libraries":{"System.CommandLine/3.0.0":[]}}""")]
    public static void Tool_dependency_manifest_that_does_not_list_its_libraries_is_rejected(string manifest)
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe")),
            (ToolDependencyManifest.EntryPath, manifest));
        Action read = () => ToolPackageCheck.ReadCarriedPackages(tool);

        // Act
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldStartWith(tool + ": invalid " + ToolDependencyManifest.EntryPath + ": ", StringComparison.Ordinal);
    }

    [Fact]
    public static void Tool_commands_are_read_from_the_framework_dependent_settings_of_every_target_framework()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            ("tools/net8.0/any/DotnetToolSettings.xml", DotnetToolSettings.Declaring("sharedkernel-probe")),
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring("sharedkernel-probe", "sharedkernel-probe-admin")),
            ("tools/net10.0/linux-x64/DotnetToolSettings.xml", DotnetToolSettings.Declaring("sharedkernel-probe-native")));

        // Act
        var commands = ToolPackageCheck.ReadCommandNames(tool);

        // Assert
        commands.ShouldBe(["sharedkernel-probe", "sharedkernel-probe-admin"], "Only framework-dependent settings name the commands that a tool path install creates.");
    }

    [Fact]
    public static void Tool_settings_that_declare_a_document_type_are_rejected()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, """
                <?xml version="1.0" encoding="utf-8"?>
                <!DOCTYPE DotNetCliTool [<!ENTITY command "sharedkernel-probe">]>
                <DotNetCliTool Version="1"><Commands><Command Name="&command;" EntryPoint="Probe.Tool.dll" Runner="dotnet" /></Commands></DotNetCliTool>
                """));
        Action read = () => ToolPackageCheck.ReadCommandNames(tool);

        // Act
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldStartWith(tool + ": invalid " + DotnetToolSettings.EntryPath, StringComparison.Ordinal);
        exception.InnerException.ShouldBeOfType<XmlException>();
    }

    [Fact]
    public static void Tool_package_without_framework_dependent_settings_is_rejected()
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            ("tools/net10.0/linux-x64/DotnetToolSettings.xml", DotnetToolSettings.Declaring("sharedkernel-probe")));
        Action read = () => ToolPackageCheck.ReadCommandNames(tool);

        // Act
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldBe(tool + ": expected a command in tools/<tfm>/any/DotnetToolSettings.xml");
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData(".sharedkernel-probe")]
    [InlineData("../sharedkernel-probe")]
    [InlineData("tools/sharedkernel-probe")]
    [InlineData("tools\\sharedkernel-probe")]
    public static void Tool_command_names_that_are_not_plain_file_names_are_rejected(string command)
    {
        // Arrange
        using var temporary = new TemporaryReleasePrepDirectory();
        var tool = NuGetPackageBuilder.WriteToolPackage(
            temporary.PackageDirectory,
            "PandaBear.SharedKernel.Probe.Tool",
            "1.0.0",
            (DotnetToolSettings.EntryPath, DotnetToolSettings.Declaring(command)));
        Action read = () => ToolPackageCheck.ReadCommandNames(tool);

        // Act
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldBe($"{tool}: invalid command name '{command}' in {DotnetToolSettings.EntryPath}");
    }
}
