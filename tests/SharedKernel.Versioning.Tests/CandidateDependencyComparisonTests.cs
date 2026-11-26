using System.Text.Json.Nodes;
using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

/// <summary>Verifies comparison of NuGet and bundled tool metadata from exact candidate archives.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CandidateDependencyComparisonTests
{
    private const string Command = "compare-candidate-dependencies";
    private const string BaselineVersion = "1.0.0-alpha.1";
    private const string CandidateVersion = "1.0.0-alpha.2";
    private const string BaselineRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CandidateRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Probe = "PandaBear.SharedKernel.Probe";
    private const string Tool = "PandaBear.SharedKernel.Probe.Tool";

    [Fact]
    public static async Task Coordinated_versions_leave_an_empty_deterministic_dependency_report()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var framework = TestTargetFramework.Moniker;
        baseline.WriteGroupedPackage(Probe, BaselineVersion, $"""
            <group targetFramework="{framework}">
              <dependency id="PandaBear.SharedKernel.Other" version="{BaselineVersion}" />
              <dependency id="PandaBear.SharedKernel.Exact" version="[{BaselineVersion}]" />
              <dependency id="SharedKernel.External" version="{BaselineVersion}" />
            </group>
            """);
        candidate.WriteGroupedPackage(Probe, CandidateVersion, $"""
            <group targetFramework="{framework}">
              <dependency id="PandaBear.SharedKernel.Exact" version="[{CandidateVersion}]" />
              <dependency id="PandaBear.SharedKernel.Other" version="{CandidateVersion}" />
              <dependency id="SharedKernel.External" version="{BaselineVersion}" />
            </group>
            """);
        var earlier = await baseline.WriteManifest(BaselineVersion, BaselineRevision, ct);
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");
        var secondReport = Path.Combine(candidate.DirectoryPath, "second-report.json");
        var runner = new RecordingProcessRunner();
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, runner);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);
        var repeat = await VersioningCommandLineRunner.Invoke(command,
            [Command, "--baseline", earlier, "--candidate", newer, "--output", secondReport], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        repeat.ExitCode.ShouldBe(0, repeat.Error);
        runner.Processes.ShouldBeEmpty();
        var text = await File.ReadAllTextAsync(report, ct);
        text.ShouldBe(await File.ReadAllTextAsync(secondReport, ct));
        var evidence = JsonNode.Parse(text).ShouldNotBeNull();
        evidence["status"]!.GetValue<string>().ShouldBe("passed");
        evidence["packages"]!.AsArray().ShouldBeEmpty();
        evidence["baseline"]!["manifestSha256"]!.GetValue<string>().ShouldBe(await CandidateManifest.HashPackage(earlier, ct));
        evidence["candidate"]!["manifestSha256"]!.GetValue<string>().ShouldBe(await CandidateManifest.HashPackage(newer, ct));
        evidence["candidate"]!["libraryRevision"]!.GetValue<string>().ShouldBe(CandidateRevision);
    }

    [Fact]
    public static async Task Framework_groups_and_unexpected_ranges_and_qualifiers_are_reported()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var framework = TestTargetFramework.Moniker;
        baseline.WriteGroupedPackage(Probe, BaselineVersion, $"""
            <group targetFramework="{framework}">
              <dependency id="External.Pkg" version="[3.0,4.0)" include="Runtime" exclude="Build" />
              <dependency id="PandaBear.SharedKernel.Skew" version="{BaselineVersion}.1" />
              <dependency id="PandaBear.SharedKernel.Range" version="[{BaselineVersion},2.0)" />
            </group>
            <group targetFramework="netstandard2.0" />
            """);
        candidate.WriteGroupedPackage(Probe, CandidateVersion, $"""
            <group targetFramework="{framework}">
              <dependency id="External.Pkg" version="[3.0,5.0)" include="Compile" exclude="Analyzers" />
              <dependency id="PandaBear.SharedKernel.Skew" version="{CandidateVersion}.1" />
              <dependency id="PandaBear.SharedKernel.Range" version="[{CandidateVersion},2.0)" />
            </group>
            <group targetFramework="netstandard2.1" />
            """);
        var earlier = await baseline.WriteManifest(BaselineVersion, BaselineRevision, ct);
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(3, result.Error);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        var package = evidence["packages"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        var groups = package["dependencyGroups"]!.AsArray();
        groups.Where(group => group!["targetFramework"]!.GetValue<string>() == "netstandard2.0").ShouldHaveSingleItem()!["status"]!
            .GetValue<string>().ShouldBe("removed");
        groups.Where(group => group!["targetFramework"]!.GetValue<string>() == "netstandard2.1").ShouldHaveSingleItem()!["status"]!
            .GetValue<string>().ShouldBe("added");
        var dependencies = groups.Where(group => group!["targetFramework"]!.GetValue<string>() == framework)
            .ShouldHaveSingleItem()!["dependencies"]!.AsArray();
        dependencies.Select(item => item!["dependencyId"]!.GetValue<string>())
            .ShouldBe(["External.Pkg", "PandaBear.SharedKernel.Range", "PandaBear.SharedKernel.Skew"]);
        dependencies[0]!["baseline"]!["version"]!.GetValue<string>().ShouldBe("[3.0,4.0)");
        dependencies[0]!["candidate"]!["include"]!.GetValue<string>().ShouldBe("Compile");
        dependencies[0]!["candidate"]!["exclude"]!.GetValue<string>().ShouldBe("Analyzers");
        dependencies[2]!["candidate"]!["version"]!.GetValue<string>().ShouldBe(CandidateVersion + ".1");
    }

    [Fact]
    public static async Task Tool_runtime_libraries_and_package_inventory_report_actual_changes()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var manifest = ToolDependencyManifest.EntryPathFor(TestTargetFramework.Moniker);
        var target = AppContext.TargetFrameworkName;
        NuGetPackageBuilder.WriteToolPackage(baseline.DirectoryPath, Tool, BaselineVersion, (manifest, $$$"""
            {
              "targets": {
                "{{{target}}}": {
                  "Probe.Tool/0.0.0.0": { "dependencies": { "Old.Runtime": "3.0.0" } },
                  "Old.Runtime/3.0.0": {},
                  "SharedKernel.Other/{{{BaselineVersion}}}": {}
                }
              }
            }
            """));
        NuGetPackageBuilder.WriteToolPackage(candidate.DirectoryPath, Tool, CandidateVersion, (manifest, $$$"""
            {
              "targets": {
                "{{{target}}}": {
                  "Probe.Tool/0.0.0.0": { "dependencies": { "New.Runtime": "4.0.0" } },
                  "New.Runtime/4.0.0": {},
                  "SharedKernel.Other/{{{CandidateVersion}}}": {}
                }
              }
            }
            """));
        var removed = NuGetPackageBuilder.WritePackage(baseline.DirectoryPath, "PandaBear.SharedKernel.Removed", BaselineVersion);
        var added = NuGetPackageBuilder.WritePackage(candidate.DirectoryPath, "PandaBear.SharedKernel.Added", CandidateVersion);
        var earlier = await baseline.WriteManifest(BaselineVersion, BaselineRevision, ct);
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(3, result.Error);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        var packages = evidence["packages"]!.AsArray();
        packages.Select(package => package!["status"]!.GetValue<string>()).ShouldBe(["added", "changed", "removed"]);
        evidence["baseline"]!["packageSha256"]![Path.GetFileName(removed)].ShouldNotBeNull();
        evidence["candidate"]!["packageSha256"]![Path.GetFileName(added)].ShouldNotBeNull();
        var runtime = packages[1]!["toolRuntimeTargets"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        runtime["runtimeManifest"]!.GetValue<string>().ShouldBe(manifest);
        runtime["target"]!.GetValue<string>().ShouldBe(target);
        var libraries = runtime["libraries"]!.AsArray();
        libraries.Select(library => library!["libraryId"]!.GetValue<string>()).ShouldBe(["New.Runtime", "Old.Runtime", "Probe.Tool"]);
        libraries.Select(library => library!["status"]!.GetValue<string>()).ShouldBe(["added", "removed", "changed"]);
    }

    [Theory]
    [InlineData(true, "added", "candidate", "baseline")]
    [InlineData(false, "removed", "baseline", "candidate")]
    public static async Task An_added_or_removed_package_keeps_its_ungrouped_dependency_evidence(
        bool added, string status, string present, string absent)
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        const string packageId = "PandaBear.SharedKernel.Declared";
        baseline.WriteGroupedPackage(Probe, BaselineVersion, string.Empty);
        candidate.WriteGroupedPackage(Probe, CandidateVersion, string.Empty);
        var feed = added ? candidate : baseline;
        var version = added ? CandidateVersion : BaselineVersion;
        feed.WriteGroupedPackage(packageId, version, """
            <dependency id="External.Library" version="[2.0,3.0)" include="Compile" exclude="Runtime" />
            """);
        var earlier = await baseline.WriteManifest(BaselineVersion, BaselineRevision, ct);
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(3, result.Error);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        var package = evidence["packages"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        package["packageId"].ShouldNotBeNull().GetValue<string>().ShouldBe(packageId);
        package["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        var group = package["dependencyGroups"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        group.AsObject().ContainsKey("targetFramework").ShouldBeTrue();
        group["targetFramework"].ShouldBeNull();
        group["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        var edge = group["dependencies"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        edge["dependencyId"].ShouldNotBeNull().GetValue<string>().ShouldBe("External.Library");
        edge["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        edge[absent].ShouldBeNull();
        var declaration = edge[present].ShouldNotBeNull();
        declaration["version"].ShouldNotBeNull().GetValue<string>().ShouldBe("[2.0,3.0)");
        declaration["include"].ShouldNotBeNull().GetValue<string>().ShouldBe("Compile");
        declaration["exclude"].ShouldNotBeNull().GetValue<string>().ShouldBe("Runtime");
    }

    [Theory]
    [InlineData(true, "added", "candidate", "baseline")]
    [InlineData(false, "removed", "baseline", "candidate")]
    public static async Task An_added_or_removed_tool_keeps_its_bundled_runtime_dependency_evidence(
        bool added, string status, string present, string absent)
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var manifest = ToolDependencyManifest.EntryPathFor(TestTargetFramework.Moniker);
        var target = AppContext.TargetFrameworkName + "/linux-x64";
        baseline.WriteGroupedPackage(Probe, BaselineVersion, string.Empty);
        candidate.WriteGroupedPackage(Probe, CandidateVersion, string.Empty);
        var feed = added ? candidate : baseline;
        var version = added ? CandidateVersion : BaselineVersion;
        NuGetPackageBuilder.WriteToolPackage(feed.DirectoryPath, Tool, version, (manifest, $$$"""
            {
              "targets": {
                "{{{target}}}": {
                  "Probe.Tool/1.0.0": { "dependencies": { "External.Library": "2.1.0" } }
                }
              }
            }
            """));
        var earlier = await baseline.WriteManifest(BaselineVersion, BaselineRevision, ct);
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(3, result.Error);
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        var package = evidence["packages"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        package["packageId"].ShouldNotBeNull().GetValue<string>().ShouldBe(Tool);
        package["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        var runtime = package["toolRuntimeTargets"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        runtime["runtimeManifest"].ShouldNotBeNull().GetValue<string>().ShouldBe(manifest);
        runtime["target"].ShouldNotBeNull().GetValue<string>().ShouldBe(target);
        runtime["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        var library = runtime["libraries"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        library["libraryId"].ShouldNotBeNull().GetValue<string>().ShouldBe("Probe.Tool");
        library["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        library[absent + "Version"].ShouldBeNull();
        library[present + "Version"].ShouldNotBeNull().GetValue<string>().ShouldBe("1.0.0");
        var edge = library["dependencies"].ShouldNotBeNull().AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        edge["dependencyId"].ShouldNotBeNull().GetValue<string>().ShouldBe("External.Library");
        edge["status"].ShouldNotBeNull().GetValue<string>().ShouldBe(status);
        edge[absent].ShouldBeNull();
        edge[present].ShouldNotBeNull()["version"].ShouldNotBeNull().GetValue<string>().ShouldBe("2.1.0");
    }

    [Fact]
    public static async Task A_tampered_archive_fails_without_writing_evidence()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var earlier = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, false));
        var newer = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, false));
        var archive = Directory.EnumerateFiles(baseline.DirectoryPath, "*.nupkg").ShouldHaveSingleItem();
        await File.AppendAllTextAsync(archive, "changed archive", ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Candidate package changed", StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{"targets":[]}""", "runtime targets object")]
    [InlineData("""{"targets":{"net11.0":[]}}""", "runtime libraries object")]
    [InlineData("""{"targets":{"net11.0":{"Probe.Tool":{}}}}""", "Invalid runtime library")]
    [InlineData("""{"targets":{"net11.0":{"Probe.Tool/":{}}}}""", "Invalid runtime library")]
    [InlineData("""{"targets":{"net11.0":{"Probe.Tool/1.0.0":[]}}}""", "Invalid runtime library")]
    [InlineData("""{"targets":{"net11.0":{"Probe.Tool/1.0.0":{"dependencies":{"Probe.Library":1}}}}}""", "Invalid runtime dependency")]
    [InlineData("""{"targets":{"net11.0":{"Probe.Tool/1.0.0":{"dependencies":{"Probe.Library":""}}}}}""", "Invalid runtime dependency")]
    [InlineData("""{"targets":{"net11.0":{"Probe.Tool/1.0.0":{},"probe.tool/1.0.1":{}}}}""", "Duplicate runtime library")]
    public static async Task An_invalid_tool_runtime_manifest_fails_without_writing_evidence(string runtime, string error)
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var manifest = ToolDependencyManifest.EntryPathFor(TestTargetFramework.Moniker);
        var earlier = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, false));
        NuGetPackageBuilder.WriteToolPackage(candidate.DirectoryPath, Tool, CandidateVersion, (manifest, runtime));
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(manifest, StringComparison.Ordinal);
        result.Error.ShouldContain(error, StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Theory]
    [InlineData("""<dependency version="1.0.0" />""", "invalid NuGet dependency")]
    [InlineData("""<unexpected id="External" />""", "invalid NuGet dependency")]
    [InlineData("""<dependency id="External" /><dependency id="external" />""", "duplicate NuGet dependency 'external'")]
    [InlineData("""<group targetFramework="net11.0" /><group targetFramework="NET11.0" />""", "duplicate NuGet dependency group")]
    public static async Task Ambiguous_or_invalid_package_dependencies_fail_without_writing_evidence(string declarations, string error)
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var earlier = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, false));
        candidate.WriteGroupedPackage(Probe, CandidateVersion, declarations);
        var newer = await candidate.WriteManifest(CandidateVersion, CandidateRevision, ct);
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain(error, StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
    }

    [Fact]
    public static async Task An_existing_dependency_report_is_preserved()
    {
        // Arrange
        using var baseline = new CandidateComparisonTestFeed();
        using var candidate = new CandidateComparisonTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var earlier = await baseline.Write(BaselineVersion, BaselineRevision, ct, (Probe, false));
        var newer = await candidate.Write(CandidateVersion, CandidateRevision, ct, (Probe, false));
        var report = Path.Combine(candidate.DirectoryPath, "dependency-comparison.json");
        const string priorEvidence = """{"status":"reviewed"}""";
        await File.WriteAllTextAsync(report, priorEvidence, ct);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(
            [Command, "--baseline", earlier, "--candidate", newer, "--output", report], ct);
        var retainedEvidence = await File.ReadAllTextAsync(report, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("report path already exists", StringComparison.Ordinal);
        retainedEvidence.ShouldBe(priorEvidence);
    }
}
