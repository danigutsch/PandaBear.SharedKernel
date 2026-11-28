using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

/// <summary>Exercises opt-in source provenance through the Versioning command line without network or child processes.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CandidateSourceLinkTests
{
    [Theory]
    [InlineData(null, false, 3, "missing-source-link")]
    [InlineData(CandidateSourceLinkTestFeed.OtherRevision, false, 3, "source-link-mismatch")]
    [InlineData(CandidateSourceLinkTestFeed.CandidateRevision, true, 3, "assembly-mismatch")]
    [InlineData(CandidateSourceLinkTestFeed.CandidateRevision, false, 0, "passed")]
    public static async Task Command_checks_symbols_against_the_assembly_and_source_revision(
        string? sourceRevision, bool mismatchedAssembly, int exit, string status)
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(sourceRevision, mismatchedAssembly, ct);
        var manifestHash = await CandidateManifest.HashPackage(manifest, ct);
        var symbolPath = Path.Combine(feed.DirectoryPath, CandidateSourceLinkTestFeed.SymbolFileName);
        var symbolHash = await CandidateManifest.HashPackage(symbolPath, ct);
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");
        var runner = new RecordingProcessRunner();
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, runner);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", CandidateSourceLinkTestFeed.SourcePattern, "--output", report], ct);
        var manifestAfter = await CandidateManifest.HashPackage(manifest, ct);
        var symbolAfter = await CandidateManifest.HashPackage(symbolPath, ct);

        // Assert
        result.ExitCode.ShouldBe(exit, result.Error);
        runner.Processes.ShouldBeEmpty();
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        evidence["status"]!.GetValue<string>().ShouldBe(exit == 0 ? "passed" : "differences");
        evidence["candidate"]!["manifestSha256"]!.GetValue<string>().ShouldBe(manifestHash);
        evidence["candidate"]!["libraryRevision"]!.GetValue<string>().ShouldBe(CandidateSourceLinkTestFeed.CandidateRevision);
        evidence["toolPackages"]!.AsArray().ShouldBeEmpty();
        evidence["symbolSha256"]![CandidateSourceLinkTestFeed.SymbolFileName]!.GetValue<string>().ShouldBe(symbolHash);
        var symbol = evidence["symbols"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull();
        symbol["archive"]!.GetValue<string>().ShouldBe(CandidateSourceLinkTestFeed.SymbolFileName);
        symbol["member"]!.GetValue<string>().ShouldBe($"lib/{TestTargetFramework.Moniker}/Probe.pdb");
        symbol["status"]!.GetValue<string>().ShouldBe(status);
        manifestAfter.ShouldBe(manifestHash);
        symbolAfter.ShouldBe(symbolHash);
    }

    [Theory]
    [InlineData("Probe.dll")]
    [InlineData("Probe.pdb")]
    public static async Task Command_refuses_an_ambiguous_selected_tool_member(string duplicateMember)
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false,
            ct, includeTool: true, duplicateToolMember: duplicateMember);
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");
        var runner = new RecordingProcessRunner();
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, runner);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", CandidateSourceLinkTestFeed.SourcePattern,
                "--tool-package", CandidateSourceLinkTestFeed.ToolPackageId, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(2, result.Error);
        result.Error.ShouldContain("duplicate assembly or symbol member paths", StringComparison.Ordinal);
        File.Exists(report).ShouldBeFalse();
        runner.Processes.ShouldBeEmpty();
    }

    [Fact]
    public static async Task Command_binds_the_requested_pattern_and_selected_tool_in_the_passing_report()
    {
        // Arrange
        using var feed = new CandidateSourceLinkTestFeed();
        var ct = TestContext.Current.CancellationToken;
        var (manifest, symbols) = await feed.Write(CandidateSourceLinkTestFeed.CandidateRevision, mismatchedAssembly: false, ct, includeTool: true);
        var report = Path.Combine(feed.DirectoryPath, "source-link-report.json");
        var pattern = CandidateSourceLinkTestFeed.SourcePattern;
        var expectedPatternHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pattern)));
        var runner = new RecordingProcessRunner();
        var command = VersioningCommandLine.Create(TextReader.Null, TimeProvider.System, runner, runner);

        // Act
        var result = await VersioningCommandLineRunner.Invoke(command,
            ["verify-candidate-source-link", "--candidate", manifest, "--symbols", symbols,
                "--source-url-pattern", pattern, "--tool-package", CandidateSourceLinkTestFeed.ToolPackageId, "--output", report], ct);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        runner.Processes.ShouldBeEmpty();
        var evidence = JsonNode.Parse(await File.ReadAllTextAsync(report, ct)).ShouldNotBeNull();
        evidence["sourceUrlPatternSha256"]!.GetValue<string>().ShouldBe(expectedPatternHash);
        evidence["toolPackages"]!.AsArray().ShouldHaveSingleItem().ShouldNotBeNull().GetValue<string>()
            .ShouldBe(CandidateSourceLinkTestFeed.ToolPackageId);
        evidence["symbols"]!.AsArray().Select(static row => row!["member"]!.GetValue<string>()).ShouldBe(
            [$"lib/{TestTargetFramework.Moniker}/Probe.pdb", $"tools/{TestTargetFramework.Moniker}/any/Probe.pdb"]);
    }
}
